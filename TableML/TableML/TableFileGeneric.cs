using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;

namespace TableML
{
    //表格读取的核心基础类，可以设置泛型(T是一行的类，继承TableFileRow)。包含所有HeaderInfo，TableFileRow
    public class TableFile<T> : IDisposable, IEnumerable<TableFileRow> where T : TableFileRow, new()
    {
        private readonly TableFileConfig _config;

        protected internal int _colCount;  // 列数

        //HeaderName——表头信息HeaderInfo
        public readonly Dictionary<string, HeaderInfo> Headers = new Dictionary<string, HeaderInfo>();

        //RowId行号——每一行分割后的string
        protected internal Dictionary<int, string[]> TabInfo = new Dictionary<int, string[]>();

        //RowId行号——TableFileRow。IOS不支持 Dict<int, T>
        protected internal Dictionary<int, TableFileRow> Rows = new Dictionary<int, TableFileRow>();

        //每一行的Key——TableFileRow
        protected Dictionary<object, TableFileRow> PrimaryKey2Row = new Dictionary<object, TableFileRow>();

        //Headers.Keys
        public Dictionary<string, HeaderInfo>.KeyCollection HeaderNames
        {
            get
            {
                return Headers.Keys;
            }
        }

        public TableFile(string[] contents)
            : this(new TableFileConfig()
            {
                Contents = contents
            })
        {
        }

        public TableFile()
            : this(new TableFileConfig())
        {
        }

        public TableFile(TableFileConfig config)
        {
            _config = config;
            ParseAll(config);
        }

        //用FileStream读取文件，ParseAll解析到TableFileConfig
        public TableFile(string fileFullPath, Encoding encoding)
        {
            // 不会锁死, 允许其它程序打开
            using (FileStream fileStream = new FileStream(fileFullPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                var config = new TableFileConfig()
                {
                    ContentStreams = new Stream[] { fileStream },
                };

                if (encoding != null)
                {
                    config.Encoding = encoding;
                }

                _config = config;

                ParseAll(_config);
            }
        }

        private void ParseAll(TableFileConfig config)
        {
            ParseStringsArray(config.Contents);
            ParseStreamsArray(config.ContentStreams);
        }

        //遍历TableFileConfig.ContentStreams，ParseStream解析每个Stream
        private void ParseStreamsArray(Stream[] contentStreams)
        {
            if (contentStreams != null)
            {
                for (var i = 0; i < _config.ContentStreams.Length; i++)
                {
                    var stream = _config.ContentStreams[i];
                    ParseStream(stream);
                }
            }
        }

        //ParseReader
        protected bool ParseStream(Stream stream)
        {
            if (stream != null)
            {
                using (var oReader = new StreamReader(stream, _config.Encoding))
                {
                    ParseReader(oReader);
                }
                return true;
            }

            return false;
        }

        //遍历TableFileConfig.Contents，ParseString解析每个string
        protected void ParseStringsArray(string[] contents)
        {
            if (contents != null)
            {
                for (var i = 0; i < _config.Contents.Length; i++)
                {
                    var content = _config.Contents[i];

                    ParseString(content);
                }
            }
        }

        //ParseReader
        protected bool ParseString(string content)
        {
            if (!string.IsNullOrEmpty(content))
            {
                using (var oReader = new StringReader(content))
                {
                    ParseReader(oReader);
                }

                return true;
            }

            return false;
        }

        //关键方法。最终都是调用该方法来解析。读取Reader的行数递增索引。从第1行开始。除了1和2两行是定义和声明，其他的都用一个TableFileRow来保存
        private int _rowIndex = 1;
        protected bool ParseReader(TextReader oReader)
        {
            //1. 首行。每一项保存到HeaderInfo.HeaderName
            var headLine = oReader.ReadLine();
            if (headLine == null)
            {
                OnException(TableFileExceptionType.HeadLineNull);
                return false;
            }

            //2. 声明行。每一项保存到HeaderInfo.HeaderMeta
            var metaLine = oReader.ReadLine(); 
            if (metaLine == null)
            {
                OnException(TableFileExceptionType.MetaLineNull);
                return false;
            }

            //首行是每一项的名称，先读取到HeaderInfo
            string[] firstLineSplitString = headLine.Split(_config.Separators, StringSplitOptions.None);  // don't remove RemoveEmptyEntries!
            string[] firstLineDef = new string[firstLineSplitString.Length];

            //将声明行的数据拷贝firstLineDef，确保不会超出表头的。再赋值给HeaderInfo
            var metaLineArr = metaLine.Split(_config.Separators, StringSplitOptions.None);
            Array.Copy(metaLineArr, 0, firstLineDef, 0, metaLineArr.Length);

            for (int i = 0; i < firstLineSplitString.Length; i++)
            {
                var headerString = firstLineSplitString[i];

                //创建一个HeaderInfo
                var headerInfo = new HeaderInfo
                {
                    ColumnIndex = i,
                    HeaderName = headerString,
                    HeaderMeta = firstLineDef[i],
                };

                Headers[headerInfo.HeaderName] = headerInfo;
            }

            //列数
            _colCount = firstLineSplitString.Length;

            //3. 剩余的行内容，创建TableFileRow来保存
            string sLine = "";
            while (sLine != null)
            {
                sLine = oReader.ReadLine();
                if (sLine != null)
                {
                    string[] splitString1 = sLine.Split(_config.Separators, StringSplitOptions.None);

                    TabInfo[_rowIndex] = splitString1;

                    //创建一行的数据TableFileRow
                    T newT = new T();
                    newT.Ctor(_rowIndex, Headers);
                    newT.Values = splitString1;

                    if (!newT.IsAutoParse)
                        //非自动解析
                        newT.Parse(splitString1);
                    else
                        //自动解析，反射，赋值给TableFileRow
                        AutoParse(newT, splitString1);

                    if (newT.GetPrimaryKey() != null)
                    {
                        TableFileRow oldT;
                        if (!PrimaryKey2Row.TryGetValue(newT.GetPrimaryKey(), out oldT))  
                        {
                            // 原本不存在，使用new的，释放cacheNew，下次直接new
                            PrimaryKey2Row[newT.GetPrimaryKey()] = newT;
                        }
                        else  
                        {
                            // 原本存在，说明表里有多个PrimaryKey，报个错
                            TableFileRow toT = oldT;

                            OnException(TableFileExceptionType.DuplicatedKey, toT.GetPrimaryKey().ToString());

                            // 使用原来的，不使用新new出来的, 下回合直接用_cachedNewObj
                            newT = (T)toT;
                        }
                    }

                    //TableFileRow保存到Rows
                    Rows[_rowIndex] = newT;
                    _rowIndex++;
                }
            }

            return true;
        }

        //用反射获取TableFileRow类的所有成员
        internal FieldInfo[] AutoTabFields
        {
            get
            {
                return typeof(TableFileRow).GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            }
        }

        //internal PropertyInfo[] TabProperties
        //{
        //    get
        //    {
        //        List<PropertyInfo> props = new List<PropertyInfo>();
        //        foreach (var fieldInfo in typeof(T).GetProperties())
        //        {
        //            if (fieldInfo.GetCustomAttributes(typeof(TabColumnAttribute), true).Length > 0)
        //            {
        //                props.Add(fieldInfo);
        //            }
        //        }
        //        return props.ToArray();
        //    }
        //}

        //解析TableFileRow类的所有成员。将cellStrs数据反射保存到TableFileRow
        protected void AutoParse(TableFileRow tableRow, string[] cellStrs)
        {
            var type = tableRow.GetType();
            var okFields = new List<FieldInfo>();

            //遍历TableFileRow的所有成员，容错
            foreach (FieldInfo field in AutoTabFields)
            {
                if (!HasColumn(field.Name))
                {
                    //类中没有这个成员名称，报错
                    OnException(TableFileExceptionType.NotFoundHeader, type.Name, field.Name);
                    continue;
                }
                okFields.Add(field);
            }

            //遍历所有成员
            foreach (var field in okFields)
            {
                //成员名称
                var fieldName = field.Name;
                var fieldType = field.FieldType;

                //判断该成员是否有Get方法
                var methodName = string.Format("Get_{0}", fieldType.Name);
                var method = type.GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
                if (method != null)
                {
                    //有Get方法

                    // FieldName所在索引
                    int index = Headers[fieldName].ColumnIndex;

                    //该成员默认值。如果第二行的声明，根据，分割后有两项，则第一项是默认值
                    string defaultValue = "";
                    var headerDef = Headers[fieldName].HeaderMeta;
                    if (!string.IsNullOrEmpty(headerDef))
                    {
                        var defs = headerDef.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                        //if (defs.Length >= 1) szType = defs[0];
                        if (defs.Length >= 2) defaultValue = defs[1];
                    }

                    //调用TableFileRow的Get方法，数据保存
                    field.SetValue(tableRow, method.Invoke(tableRow, new object[]
                    {
                        //数据
                       cellStrs[index] , defaultValue
                    }));
                }
                else
                {
                    //没有Get方法，报错
                    OnException(TableFileExceptionType.NotFoundGetMethod, methodName);
                }
            }

        }

        //Headers.ContainsKey
        public bool HasColumn(string colName)
        {
            return Headers.ContainsKey(colName);
        }

        protected internal void OnException(TableFileExceptionType message, params string[] args)
        {
            if (TableFileStaticConfig.GlobalExceptionEvent != null)
            {
                TableFileStaticConfig.GlobalExceptionEvent(message, args);
            }

            if (_config.OnExceptionEvent != null)
            {
                _config.OnExceptionEvent(message, args);
            }

            if (TableFileStaticConfig.GlobalExceptionEvent == null && _config.OnExceptionEvent == null)
            {
                string[] argsStrs = new string[args.Length];
                for (var i = 0; i < argsStrs.Length; i++)
                {
                    var arg = args[i];
                    if (arg == null) continue;
                    argsStrs[i] = arg.ToString();
                }
                throw new Exception(string.Format("{0} - {1}", message, string.Join("|", argsStrs)));
            }
        }

        //Rows.Count
        public int GetRowCount()
        {
            return Rows.Count;
        }

        public int GetColumnCount()
        {
            return _colCount;
        }

        public int GetWidth()
        {
            return _colCount;
        }

        //获取某一行的TableFileRow
        public T GetRow(int row)
        {
            TableFileRow rowT;
            if (!Rows.TryGetValue(row, out rowT))
            {
                OnException(TableFileExceptionType.NotFoundRow, row.ToString());
                return null;
            }

            return (T)rowT;
        }

        public void Dispose()
        {
            Headers.Clear();
            TabInfo.Clear();
            Rows.Clear();
            PrimaryKey2Row.Clear();
        }

        public void Close()
        {
            Dispose();
        }

        //PrimaryKey2Row中是否有primaryKey
        public bool HasPrimaryKey(object primaryKey)
        {
            return PrimaryKey2Row.ContainsKey(primaryKey);
        }

        //根据primaryKey从PrimaryKey2Row中获取一行的数据TableFileRow
        public virtual T FindByPrimaryKey(object primaryKey, bool throwError = true)
        {
            TableFileRow ret;

            if (PrimaryKey2Row.TryGetValue(primaryKey, out ret))
                return (T)ret;
            else
            {
                if (throwError)
                    OnException(TableFileExceptionType.NotFoundPrimaryKey, primaryKey.ToString());
                return null;
            }
        }

        public T GetByPrimaryKey(object primaryKey)
        {
            return FindByPrimaryKey(primaryKey);
        }

        // Rows.Values
        public IEnumerable<TableFileRow> GetAll()
        {
            return Rows.Values;
        }

        public IEnumerator<TableFileRow> GetEnumerator()
        {
            return Rows.Values.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return Rows.Values.GetEnumerator();
        }


        ///---------------------------Static

        // 直接从字符串分析。返回一个TableFile<T>，并且解析好所有数据
        public static TableFile<T> LoadFromString(params string[] contents)
        {
            TableFile<T> tabFile = new TableFile<T>(contents);

            return tabFile;
        }

        // 直接从文件, 传入完整目录，
        //跟通过资源管理器自动生成完整目录不一样，给art库用的
        public static TableFile<T> LoadFromFile(string fileFullPath, Encoding encoding = null)
        {
            return new TableFile<T>(fileFullPath, encoding);
        }

    }
}
