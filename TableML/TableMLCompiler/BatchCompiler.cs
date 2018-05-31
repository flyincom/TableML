using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using DotLiquid;

namespace TableML.Compiler
{
	public class BatchCompiler
	{
		/// <summary>
		/// 是否自动在编译配置表时生成静态代码，如果不需要，外部设置false
		/// </summary>
		//public static bool AutoGenerateCode = true;

		/// 当生成的类名，包含数组中字符时，不生成代码。比如
		/// GenerateCodeFilesFilter = new []
		/// {
		///     "SubdirSubSubDirExample3",
		/// };
		public string[] GenerateCodeFilesFilter = null;

		//条件编译变量
		public string[] CompileSettingConditionVars;

        /// 可以为模板提供额外生成代码块！返回string即可！自定义[InitializeOnLoad]的类并设置这个委托
        public CustomExtraStringDelegate CustomExtraString;
		public delegate string CustomExtraStringDelegate(TableCompileResult tableCompileResult);

		void GenerateCode(string templateString, string genCodeFilePath, string nameSpace, List<Hash> files)
		{

			var codeTemplates = new Dictionary<string, string>()
			{
				{templateString, genCodeFilePath},
			};

			foreach (var kv in codeTemplates)
			{
				var templateStr = kv.Key;
				var exportPath = kv.Value;

				// 生成代码
				var template = Template.Parse(templateStr);
				var topHash = new Hash();
				topHash["NameSpace"] = nameSpace;
				topHash["Files"] = files;

				if (!string.IsNullOrEmpty(exportPath))
				{
					var genCode = template.Render(topHash);
					if (File.Exists(exportPath)) // 存在，比较是否相同
					{
						if (File.ReadAllText(exportPath) != genCode)
						{
							//EditorUtility.ClearProgressBar();
							// 不同，会触发编译，强制停止Unity后再继续写入
							//if (EditorApplication.isPlaying)
							{
								Console.WriteLine("[CAUTION]AppSettings code modified! Force stop Unity playing");
								//EditorApplication.isPlaying = false;
							}
							File.WriteAllText(exportPath, genCode);
						}
					}
					else
						File.WriteAllText(exportPath, genCode);

				}
			}
			// make unity compile
			//AssetDatabase.Refresh();
		}

        //主要接口。开始打包流程
		public List<TableCompileResult> CompileTableMLAll(
            string sourcePath,                      //表根目录
            string compilePath,                     //输出目录
            string genCodeFilePath,                 //生成代码目录
            string genCodeTemplateString = null,    //模板
            string nameSpace = "AppSettings",       //命名空间
            string changeExtension = ".tml",        //后缀
            string settingCodeIgnorePattern = null, //代码不管布局
            bool forceAll = false                   //
            )
		{
			var results = new List<TableCompileResult>();
			var compileBaseDir = compilePath;

            //创建一个Compiler
            var compiler = new Compiler(new CompilerConfig() { ConditionVars = CompileSettingConditionVars });

            //Excel表的后缀
			var excelExt = new HashSet<string>() { ".xls", ".xlsx", ".tsv" };

            //导出表的后缀
		    var copyExt = new HashSet<string>() {".txt"};

			var findDir = sourcePath;

			try
			{
                //获取目录下所有的文件
				var allFiles = Directory.GetFiles(findDir, "*.*", SearchOption.AllDirectories);
				var allFilesCount = allFiles.Length;
				var nowFileIndex = -1; // 开头+1， 起始为0
				foreach (var excelPath in allFiles)
				{
					nowFileIndex++;
                    //文件后缀
					var ext = Path.GetExtension(excelPath);
                    //文件名称
					var fileName = Path.GetFileNameWithoutExtension(excelPath);
                    //相对路径
                    var relativePath = excelPath.Replace(findDir, "").Replace("\\", "/");
                    if (relativePath.StartsWith("/"))
                        relativePath = relativePath.Substring(1);

					if (excelExt.Contains(ext) && !fileName.StartsWith("~"))
					{
                        //如果是Excel文件。~开头为excel临时文件，不要读

                        //输出目录
                        var compileToPath = string.Format("{0}/{1}", compileBaseDir, Path.ChangeExtension(relativePath, changeExtension));

                        //FileInfo
                        var srcFileInfo = new FileInfo(excelPath);

						Console.WriteLine("Compiling Excel to Tab..." + string.Format("{0} -> {1}", excelPath, compileToPath));

						// 如果输出目录已经存在该文件，判断修改时间是否一致，用此来判断是否无需compile，节省时间
						bool doCompile = true;
						if (File.Exists(compileToPath))
						{
							var toFileInfo = new FileInfo(compileToPath);

							if (!forceAll && srcFileInfo.LastWriteTime == toFileInfo.LastWriteTime)
							{
								//Log.DoLog("Pass!SameTime! From {0} to {1}", excelPath, compileToPath);
								doCompile = false;
							}
						}

						if (doCompile)
						{
							Console.WriteLine("[SettingModule]Compile from {0} to {1}", excelPath, compileToPath);

                            //Compiler.Compile
                            var compileResult = compiler.Compile(excelPath, compileToPath, compileBaseDir, doCompile);

							// 添加模板值
							results.Add(compileResult);

							var compiledFileInfo = new FileInfo(compileToPath);
							compiledFileInfo.LastWriteTime = srcFileInfo.LastWriteTime;

						}
					}
                    else if (copyExt.Contains(ext))
                    {
                        //如果是txt文件，直接拷贝
                        var compileToPath = string.Format("{0}/{1}", compileBaseDir,
							relativePath);
                        var compileToDir = Path.GetDirectoryName(compileToPath);
                        if (!Directory.Exists(compileToDir))
                            Directory.CreateDirectory(compileToDir);
                        File.Copy(excelPath, compileToPath, true);

						Console.WriteLine("Copy File ..." + string.Format("{0} -> {1}", excelPath, compileToPath));
                    }
				}

				// 根据模板生成所有代码,  如果不是强制重建，无需进行代码编译
				//if (!AutoGenerateCode)
				//{
				//	Log.Warning("Ignore Gen Settings code");
				//}
				//else if (!force)
				//{
				//	Log.Warning("Ignore Gen Settings Code, not a forcing compiling");
				//}
				//else
				{

					// 根据编译结果，构建vars，同class名字的，进行合并
					var templateVars = new Dictionary<string, TableTemplateVars>();
					foreach (var compileResult in results)
					{
						if (!string.IsNullOrEmpty(settingCodeIgnorePattern))
						{
							var ignoreRegex = new Regex(settingCodeIgnorePattern);
							if (ignoreRegex.IsMatch(compileResult.TabFileRelativePath))
								continue; // ignore this 
						}

						var customExtraStr = CustomExtraString != null ? CustomExtraString(compileResult) : null;

						var templateVar = new TableTemplateVars(compileResult, customExtraStr);

						// 尝试类过滤
						var ignoreThisClassName = false;
						if (GenerateCodeFilesFilter != null)
						{
							for (var i = 0; i < GenerateCodeFilesFilter.Length; i++)
							{
								var filterClass = GenerateCodeFilesFilter[i];
								if (templateVar.ClassName.Contains(filterClass))
								{
									ignoreThisClassName = true;
									break;
								}

							}
						}
						if (!ignoreThisClassName)
						{
							if (!templateVars.ContainsKey(templateVar.ClassName))
								templateVars.Add(templateVar.ClassName, templateVar);
							else
							{
								templateVars[templateVar.ClassName].RelativePaths.Add(compileResult.TabFileRelativePath);
							}
						}

					}

					// 整合成字符串模版使用的List
					var templateHashes = new List<Hash>();
					foreach (var kv in templateVars)
					{
						var templateVar = kv.Value;
						var renderTemplateHash = Hash.FromAnonymousObject(templateVar);
						templateHashes.Add(renderTemplateHash);
					}


				    if (forceAll)
				    {
                        // force 才进行代码编译
                        GenerateCode(genCodeTemplateString, genCodeFilePath, nameSpace, templateHashes);
				    }
				}

			}
			finally
			{
				//EditorUtility.ClearProgressBar();
			}

			return results;
		}
	}

    //用于liquid模板
    public class TableTemplateVars
	{
		public delegate string CustomClassNameDelegate(string originClassName, string filePath);

		/// <summary>
		/// You can custom class name
		/// </summary>
		public static TableTemplateVars.CustomClassNameDelegate CustomClassNameFunc;

		public List<string> RelativePaths = new List<string>();

		/// <summary>
		///  构建成一个数组"aaa", "bbb"
		/// </summary>
		public string TabFilePaths
		{
			get
			{
				var paths = "\"" + string.Join("\", \"", RelativePaths.ToArray()) + "\"";
				return paths;
			}
		}

		public string ClassName { get; set; }
		public List<TableColumnVars> FieldsInternal { get; set; } // column + type

		public string PrimaryKey { get; set; }

		public List<Hash> Fields
		{
			get { return (from f in FieldsInternal select Hash.FromAnonymousObject(f)).ToList(); }
		}

		//第一列的成员
		public Hash PrimaryKeyField
		{
			get { return Fields[0]; }
		}

		/// <summary>
		/// Custom extra strings
		/// </summary>
		public string Extra { get; private set; }

		public List<Hash> Columns2DefaultValus { get; set; } // column + Default Values

		public TableTemplateVars(TableCompileResult compileResult, string extraString)
			: base()
		{
			var tabFileRelativePath = compileResult.TabFileRelativePath;
			RelativePaths.Add(compileResult.TabFileRelativePath);

			ClassName = DefaultClassNameParse(tabFileRelativePath);
			// 可自定义Class Name
			if (CustomClassNameFunc != null)
				ClassName = CustomClassNameFunc(ClassName, tabFileRelativePath);

			FieldsInternal = compileResult.FieldsInternal;
			PrimaryKey = compileResult.PrimaryKey;
			Columns2DefaultValus = new List<Hash>();

			Extra = extraString;
		}

		/// <summary>
		/// get a class name from tab file path, default strategy
		/// </summary>
		/// <param name="filePath"></param>
		/// <returns></returns>
		string DefaultClassNameParse(string tabFilePath)
		{
			// 未处理路径的类名, 去掉后缀扩展名
			var classNameOrigin = Path.ChangeExtension(tabFilePath, null);

			// 子目录合并，首字母大写, 组成class name
			var className = classNameOrigin.Replace("/", "_").Replace("\\", "_");
			className = className.Replace(" ", "");
			className = string.Join("", (from name
				in className.Split(new[] { '_' }, StringSplitOptions.RemoveEmptyEntries)
										 select (name[0].ToString().ToUpper() + name.Substring(1, name.Length - 1)))
				.ToArray());

			// 去掉+或#号后面的字符
			var plusSignIndex = className.IndexOf("+");
			className = className.Substring(0, plusSignIndex == -1 ? className.Length : plusSignIndex);
			plusSignIndex = className.IndexOf("#");
			className = className.Substring(0, plusSignIndex == -1 ? className.Length : plusSignIndex);

			return className;

		}
	}

}