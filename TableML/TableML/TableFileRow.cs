using System;
using System.Collections.Generic;

namespace TableML
{
    //行类，用于TableFile类的通用行类，所有项都是string
    public partial class TableFileRow : TableRowFieldParser
    {
        //这行是第几行
        public int RowNumber { get; internal set; }

        //HeaderName——HeaderInfo。每一项
        public Dictionary<string, HeaderInfo> HeaderInfos
        {
            get; internal set;
        }

        //这行的所有值（都是string）
        public string[] Values
        {
            get; internal set;
        }

        /// <summary>
        /// 是否自动使用反射解析，自动，则使用Parse方法。目前用反射的方式
        /// </summary>
        public virtual bool IsAutoParse
        {
            get
            {
                return false;
            }
        }

        public TableFileRow()
        {
            
        }

        public TableFileRow(int rowNumber, Dictionary<string, HeaderInfo> headerInfos)
        {
            Ctor(rowNumber, headerInfos);
        }

        //避免IL2CPP striping
        internal void Ctor(int rowNumber, Dictionary<string, HeaderInfo> headerInfos)
        {
            RowNumber = rowNumber;
            HeaderInfos = headerInfos;
            Values = new string[headerInfos.Count];
        }

        //空方法
        public virtual void Parse(string[] cellStrs)
        {
            
        }

        public object PrimaryKey
        {
            get { return GetPrimaryKey(); }
        }

        //默认第一列就是PrimaryKey
        public virtual object GetPrimaryKey()
        {
            return Get(0);
        }

        //某一列的数据
        public virtual object Get(int index)
        {
            if (index > Values.Length || index < 0)
            {
                throw new Exception(string.Format("Overflow index `{0}`", index));
            }

            return Values[index];
        }

        //this[headerName]
        public string Get(string headerName)
        {
            return this[headerName];
        }

        //某一列的string数据，根据index来取
        public string this[int index]
        {
            get
            {
                return Get(index) as string;
            }
            set { Values[index] = value; }
        }

        //某一列的string数据，根据headerName来取，先获取HeaderInfo，然后根据headerInfo.ColumnIndex列来取值
        public string this[string headerName]
        {
            get
            {
                HeaderInfo headerInfo;
                if (!HeaderInfos.TryGetValue(headerName, out headerInfo))
                {
                    throw new Exception("not found header: " + headerName);
                }

                return this[headerInfo.ColumnIndex];
            }
            set
            {
                HeaderInfo headerInfo;
                if (!HeaderInfos.TryGetValue(headerName, out headerInfo))
                {
                    throw new Exception("not found header: " + headerName);
                }

                this[headerInfo.ColumnIndex] = value;
            }
        }

    }
}
