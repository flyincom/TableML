using System;
using System.Text;

namespace TableML
{
    //表类。多了自动适配number(double)和string
    public class TableObject : TableFile<TableObjectRow>
    {
        public TableObject(string content)
            : base(new string[] { content }) { }

        public TableObject(string[] contents)
            : base(contents) { }

        public TableObject() 
            : base() { }

        public TableObject(string fileFullPath, Encoding encoding) 
            : base(fileFullPath, encoding) { }

        //根据primaryKey获取行对象TableObjectRow
        public override TableObjectRow FindByPrimaryKey(object primaryKey, bool throwError = true)
        {
            if (primaryKey is short || primaryKey is int || primaryKey is long || primaryKey is decimal ||
                primaryKey is uint || primaryKey is ulong || primaryKey is ushort ||
                primaryKey is float || primaryKey is bool)
            {
                //先转成double，再调用base.FindByPrimaryKey
                return base.FindByPrimaryKey(Convert.ChangeType(primaryKey, typeof(double)), false);
            }

            //string
            return base.FindByPrimaryKey(primaryKey, throwError);

        }

        //---------------------------------------------static

        public new static TableObject LoadFromString(params string[] content)
        {
            TableObject tabFile = new TableObject(content);
            return tabFile;
        }

        public new static TableObject LoadFromFile(string fileFullPath, Encoding encoding = null)
        {
            return new TableObject(fileFullPath, encoding);
        }

    }

}