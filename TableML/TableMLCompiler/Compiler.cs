using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace TableML.Compiler
{
    //Excel转TSV
    public class Compiler
    {

        //编译时，判断格子的类型
        public enum CellType
        {
            Value,
            Comment,
            If,
            Endif
        }

        private readonly CompilerConfig _config;

        public Compiler()
            : this(new CompilerConfig(){})
        {
        }

        public Compiler(CompilerConfig cfg)
        {
            _config = cfg;
        }

        private TableCompileResult DoCompilerExcelReader(
            string path, 
            ITableSourceFile excelFile, 
            string compileToFilePath = null, 
            string compileBaseDir = null, 
            bool doCompile = true)
        {
            TableCompileResult renderVars = new TableCompileResult();
            renderVars.ExcelFile = excelFile;
            renderVars.FieldsInternal = new List<TableColumnVars>();

            StringBuilder tableBuilder = new StringBuilder();
            StringBuilder rowBuilder = new StringBuilder();
            var ignoreColumns = new HashSet<int>();

            //遍历每一列名
            foreach (var colNameStr in excelFile.ColName2Index.Keys)
            {
                var colIndex = excelFile.ColName2Index[colNameStr];
                if (!string.IsNullOrEmpty(colNameStr))
                {
                    bool isCommentColumn = CheckCellType(colNameStr) == CellType.Comment;
                    if (isCommentColumn)
                    {
                        //注释列
                        ignoreColumns.Add(colIndex);
                    }
                    else
                    {
                        //非注释列
                        if (colIndex > 0)
                            tableBuilder.Append("\t");
                        tableBuilder.Append(colNameStr);

                        string typeName = "string";
                        string defaultVal = "";

                        //这一列的所有类型
                        string[] attrs = excelFile.ColName2Statement[colNameStr].Split(new char[] {'|', '/'}, StringSplitOptions.RemoveEmptyEntries);

                        //第一个是类型名称
                        if (attrs.Length > 0)
                        {
                            typeName = attrs[0];
                        }

                        //第二个是默认值
                        if (attrs.Length > 1)
                        {
                            defaultVal = attrs[1];
                        }

                        //如果第三个是pk，说明是PrimaryKey
                        if (attrs.Length > 2)
                        {
                            if (attrs[2] == "pk")
                            {
                                renderVars.PrimaryKey = colNameStr;
                            }
                        }

                        renderVars.FieldsInternal.Add(new TableColumnVars
                        {
                            Index = colIndex - ignoreColumns.Count, // count the comment columns
                            Type = typeName,
                            Name = colNameStr,
                            DefaultValue = defaultVal,
                            Comment = excelFile.ColName2Comment[colNameStr],
                        });
                    }
                }
            }
            tableBuilder.Append("\n");

            // Statements rows, keeps
            foreach (var kv in excelFile.ColName2Statement)
            {
                var colName = kv.Key;
                var statementStr = kv.Value;

                int colIndex = excelFile.ColName2Index[colName];

                if (ignoreColumns.Contains(colIndex)) // comment column, ignore
                    continue;
                if (colIndex > 0)
                    tableBuilder.Append("\t");
                tableBuilder.Append(statementStr);
            }
            tableBuilder.Append("\n");

            // #if check, 是否正在if false模式, if false时，行被忽略
            bool ifCondtioning = true;
            if (doCompile)
            {
                // 如果不需要真编译，获取头部信息就够了
                for (var startRow = 0; startRow < excelFile.GetRowsCount(); startRow++)
                {
                    rowBuilder.Length = 0;
                    rowBuilder.Capacity = 0;
                    int columnCount = excelFile.GetColumnCount();
                    for (var loopColumn = 0; loopColumn < columnCount; loopColumn++)
                    {
                        if (!ignoreColumns.Contains(loopColumn)) // comment column, ignore 注释列忽略
                        {
                            string columnName = excelFile.Index2ColName[loopColumn];
                            string cellStr = excelFile.GetString(columnName, startRow);

                            if (loopColumn == 0)
                            {
                                CellType cellType = CheckCellType(cellStr);
                                if (cellType == CellType.Comment) // 如果行首为#注释字符，忽略这一行)
                                    break;

                                // 进入#if模式
                                if (cellType == CellType.If)
                                {
                                    string[] ifVars = GetIfVars(cellStr);
                                    bool hasAllVars = true;
                                    foreach (var var in ifVars)
                                    {
                                        if (_config.ConditionVars == null || 
                                            !_config.ConditionVars.Contains(var)) // 定义的变量，需要全部配置妥当,否则if失败
                                        {
                                            hasAllVars = false;
                                            break;
                                        }
                                    }
                                    ifCondtioning = hasAllVars;
                                    break;
                                }
                                if (cellType == CellType.Endif)
                                {
                                    ifCondtioning = true;
                                    break;
                                }

                                // 这一行被#if 忽略掉了
                                if (!ifCondtioning)
                                    break;

                                // 不是第一行，往添加换行，首列
                                if (startRow != 0)
                                    rowBuilder.Append("\n");
                            }

                            // 最后一列不需加tab
                            if (loopColumn > 0 && loopColumn < columnCount)
                                rowBuilder.Append("\t");

                            // 如果单元格是字符串，换行符改成\\n
                            cellStr = cellStr.Replace("\n", "\\n");
                            rowBuilder.Append(cellStr);

                        }
                    }

                    // 如果这行，之后\t或换行符，无其它内容，认为是可以省略的
                    if (!string.IsNullOrEmpty(rowBuilder.ToString().Trim()))
                        tableBuilder.Append(rowBuilder);
                }
            }
            
            string fileName = Path.GetFileNameWithoutExtension(path);
            string exportPath;
            if (!string.IsNullOrEmpty(compileToFilePath))
            {
                exportPath = compileToFilePath;
            }
            else
            {
                exportPath = fileName + _config.ExportTabExt;
            }

            string exportDirPath = Path.GetDirectoryName(exportPath);
            if (!Directory.Exists(exportDirPath))
                Directory.CreateDirectory(exportDirPath);

            // 是否写入文件
            if (doCompile)
                File.WriteAllText(exportPath, tableBuilder.ToString());


            // 基于base dir路径
            string tabFilePath = exportPath; // without extension
            string fullTabFilePath = Path.GetFullPath(tabFilePath).Replace("\\", "/");;
            if (!string.IsNullOrEmpty(compileBaseDir))
            {
                string fullCompileBaseDir = Path.GetFullPath(compileBaseDir).Replace("\\", "/");;
				tabFilePath = fullTabFilePath.Replace(fullCompileBaseDir, ""); // 保留后戳
            }
            if (tabFilePath.StartsWith("/"))
                tabFilePath = tabFilePath.Substring(1);

			renderVars.TabFileFullPath = fullTabFilePath;
            renderVars.TabFileRelativePath = tabFilePath;

            return renderVars;
        }

        //获取#if A B语法的变量名，返回如A B数组
        private string[] GetIfVars(string cellStr)
        {
            return cellStr.Replace("#if", "").Trim().Split(new char[] {' '}, StringSplitOptions.RemoveEmptyEntries);
        }

        //检查一个表头名，是否是可忽略的注释。或检查一个字符串
        private CellType CheckCellType(string colNameStr)
        {
            if (colNameStr.StartsWith("#if"))
                return CellType.If;
            if (colNameStr.StartsWith("#endif"))
                return CellType.Endif;
            
            foreach (var commentStartsWith in _config.CommentStartsWith)
            {
                if (colNameStr.ToLower().Trim().StartsWith(commentStartsWith.ToLower()))
                {
                    return CellType.Comment;
                }
            }

            return CellType.Value;
        }

		// Compile the specified path, auto change extension to config `ExportTabExt`
		public TableCompileResult Compile(string path)
		{
            string outputPath = System.IO.Path.ChangeExtension(path, this._config.ExportTabExt);
			return Compile(path, outputPath);
		}

        // Compile a setting file, return a hash for template
        public TableCompileResult Compile(string path, string compileToFilePath, string compileBaseDir = null, bool doRealCompile = true)
        {
			// 确保目录存在
			compileToFilePath = Path.GetFullPath(compileToFilePath);
            string compileToFileDirPath = Path.GetDirectoryName(compileToFilePath);

            if (!Directory.Exists(compileToFileDirPath))
                Directory.CreateDirectory(compileToFileDirPath);

            string ext = Path.GetExtension(path);

            ITableSourceFile sourceFile;
            if (ext == ".tsv") 
                sourceFile = new SimpleTSVFile(path);
            else 
                sourceFile = new SimpleExcelFile(path);
            
            TableCompileResult hash = DoCompilerExcelReader(path, sourceFile, compileToFilePath, compileBaseDir, doRealCompile);

            return hash;

        }
    }
}