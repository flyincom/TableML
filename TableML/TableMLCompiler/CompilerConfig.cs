namespace TableML.Compiler
{
    //编译配置。定义了：输出表的后缀，哪些符号表示注释
    public class CompilerConfig
    {
        /// 编译后的扩展名
        public string ExportTabExt = ".tml";

        // 被认为是注释的表头
        public string[] CommentStartsWith = { "Comment", "#" };

        /// 定义条件编译指令
        public string[] ConditionVars;

    }

}
