﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;

namespace TableML
{
    //Exception报错类型
    public enum TableFileExceptionType
    {
        DuplicatedKey,
        NotFoundHeader,
        HeadLineNull,
        MetaLineNull, // 第二行
        NotFoundHeadzer,
        NotFoundGetMethod,
        NotFoundPrimaryKey,
        NotFoundRow,
    }

    //每一项的定义，ColumnIndex，Name，Meta
    public class HeaderInfo
    {
        public int ColumnIndex;
        public string HeaderName;
        public string HeaderMeta;
    }

    public delegate void TableFileExceptionDelegate(TableFileExceptionType exceptionType, string[] args);

    public class TableFileStaticConfig
    {
        public static TableFileExceptionDelegate GlobalExceptionEvent;
    }

    //表元数据，Excel表先读到TableFileConfig中。包括ContentStreams，Contents，Encoding
    public class TableFileConfig
    {
        //用Stream更有效率
        public Stream[] ContentStreams;

        public string[] Contents;

        public char[] Separators = new char[] { '\t' };

        public TableFileExceptionDelegate OnExceptionEvent;

        public Encoding Encoding = Encoding.UTF8;
    }

    //表类。所有的单元格内容会被视为string类型，包括数字也是string类型
    public class TableFile : TableFile<TableFileRow>
    {
        public TableFile(string content) 
            : base(new string[] { content }) { }

        public TableFile(string[] contents) 
            : base(contents) { }

        public TableFile() 
            : base() { }

        public TableFile(string fileFullPath, Encoding encoding) 
            : base(fileFullPath, encoding) { }

        //--------------------static方法

        //创建一个TableFile
        public new static TableFile LoadFromString(params string[] content)
        {
            TableFile tabFile = new TableFile(content);
            return tabFile;
        }

        public new static TableFile LoadFromFile(string fileFullPath, Encoding encoding = null)
        {
            return new TableFile(fileFullPath, encoding);
        }
    }

}
