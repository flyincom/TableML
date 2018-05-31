namespace TableML.Compiler
{
	//表的列变量
	public class TableColumnVars
	{
		public int Index { get; set; }

        //类型
		public string Type { get; set; }

		/// 经过格式化，去掉[]的类型字符串，支持数组(int[] -> int_array), 字典(map[string]int) -> map_string_int
		public string TypeMethod
		{
			get { return Type.Replace(@"[]", "_array").Replace("<", "_").Replace(">", "").Replace(",", "_"); }
		}

		public string FormatType
		{
			get
			{
				return Type;
			}
		}

		public string Name { get; set; }
		public string DefaultValue { get; set; }
		public string Comment { get; set; }
	}

}

