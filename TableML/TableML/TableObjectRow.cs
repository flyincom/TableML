using System;
using System.Collections.Generic;

namespace TableML
{
    //行类。增加了自动转string和number
    public class TableObjectRow : TableFileRow
    {
        public TableObjectRow()
        {
        }

        public TableObjectRow(int rowNumber, Dictionary<string, HeaderInfo> headerInfos) 
            : base(rowNumber, headerInfos)
        {
        }

        //PrimaryKey
        public override object GetPrimaryKey()
        {
            var key = base.GetPrimaryKey();
            double num;
            if (key is string && double.TryParse(key.ToString(), out num))
            {
                return num;
            }

            return key;
        }

        public new object Get(int index)
        {
            return this[index];
        }

        public new object Get(string headerName)
        {
            return this[headerName];
        }

        //该行某一列的值，根据index来取
        public new object this[int index]
        {
            get
            {
                if (index > Values.Length || index < 0)
                {
                    throw new Exception(string.Format("Overflow index `{0}`", index));
                }

                var value = Values[index];
                object result;
                double number;
                if (!double.TryParse(value, out number))
                {
                    result = value;
                }
                else
                {
                    result = number;
                }

                return result;
            }
            set
            {
                Values[index] = value.ToString();
            }
        }

        //该行某一列的值，根据headerName来取，先获取HeaderInfo，然后根据headerInfo.ColumnIndex列来取值
        public new object this[string headerName]
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
