using System;
using System.Collections.Generic;
namespace TableML.Compiler
{
    //无效的Excel表，报这个错
    public class InvalidExcelException : Exception
    {
        public InvalidExcelException(string msg)
            : base(msg)
        {
        }
    }

    //编译结果。一个表一个TableCompileResult
    public class TableCompileResult
    {
        //表
        public string TabFileFullPath { get; set; }
        public string TabFileRelativePath { get; set; }

        //每一列的数据TableColumnVars
        public List<TableColumnVars> FieldsInternal { get; set; }

        public string PrimaryKey { get; set; }

        //Excel表
        public ITableSourceFile ExcelFile { get; internal set; }

        public TableCompileResult()
        {
            FieldsInternal = new List<TableColumnVars>();
        }

    }

}
