using System;
using System.IO;
using System.Text;

namespace TableML
{
    //将TableFile中的数据，写出到某个文件中。就是转表
    public class TableFileWriter : IDisposable
    {
        public readonly TableFile TabFile;

        public TableFileWriter()
        {
            TabFile = new TableFile();
        }

        public TableFileWriter(TableFile tabFile)
        {
            TabFile = tabFile;
        }

        //将TableFile中的数据拼接成字符串
        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();

            //表头
            int index = 0;
            foreach (var header in TabFile.Headers.Values)
            {
                index++;
                sb.Append(header.HeaderName);
                if (index != TabFile.Headers.Count)
                {
                    sb.Append("\t");
                }
            }
            sb.Append("\n");

            index = 0;
            foreach (var header in TabFile.Headers.Values)
            {
                index++;
                sb.Append(header.HeaderMeta);
                if (index != TabFile.Headers.Count)
                {
                    sb.Append("\t");
                }
            }
            sb.Append("\n");

            // 获取所有值
            foreach (var kv in TabFile.Rows)
            {
                var rowT = kv.Value;
                var rowItemCount = rowT.Values.Length;
                for (var i = 0; i < rowItemCount; i++)
                {
                    sb.Append(rowT.Values[i]);
                    if (i != (rowItemCount - 1))
                        sb.Append('\t'); 
                }
                sb.Append("\n");
            }

            return sb.ToString();
        }

        // 调用ToString()，将数据保存成文件
        public bool Save(string fileName)
        {
            lock (this)
            {
                bool result = false;
                try
                {
                    using (StreamWriter sw = new StreamWriter(new FileStream(fileName, FileMode.Create), System.Text.Encoding.UTF8))
                    {
                        sw.Write(ToString());

                        result = true;
                    }
                }
                catch (IOException e)
                {
                    result = false;
                    throw new Exception("可能文件正在被Excel打开?" + e.Message);
                }

                return result;
            }
        }

        //创建一个TableFileRow
        public TableFileRow NewRow()
        {
            int rowId = TabFile.Rows.Count + 1;
            var newRow = new TableFileRow(rowId, TabFile.Headers);

            TabFile.Rows.Add(rowId, newRow);

            return newRow;
        }

        public bool RemoveRow(int row)
        {
            return TabFile.Rows.Remove(row);
        }

        public TableFileRow GetRow(int row)
        {
            TableFileRow rowT;
            if (TabFile.Rows.TryGetValue(row, out rowT))
            {
                return rowT;
            }

            return null;
        }

        public int NewColumn(string colName)
        {
            return NewColumn(colName, "");
        }

        //创建一列，主要是创建一个HeaderInfo并保存到TableFile中
        public int NewColumn(string colName, string defineStr)
        {
            if (string.IsNullOrEmpty(colName))
                throw new Exception("Null Col Name : " + colName);

            var newHeader = new HeaderInfo
            {
                ColumnIndex = TabFile.Headers.Count,
                HeaderName = colName,
                HeaderMeta = defineStr,
            };

            TabFile.Headers.Add(colName, newHeader);
            TabFile._colCount++;

            return TabFile._colCount;
        }

        public void Dispose()
        {
            TabFile.Dispose();
        }
    }
}
