namespace EPLab.Compiler;

public static class ProjectTemplates
{
    public static void CreateHelloProject(string directory, bool overwrite = false, bool chinese = false)
    {
        directory = Path.GetFullPath(directory);
        if (Directory.Exists(directory) && Directory.EnumerateFileSystemEntries(directory).Any() && !overwrite)
            throw new IOException(chinese ? "文件夹不是空的。" : "The folder is not empty.");
        Directory.CreateDirectory(directory);
        string sourceDirectory = Path.Combine(directory, "src");
        Directory.CreateDirectory(sourceDirectory);

        EPLabProject project = new()
        {
            Name = "HelloEPLab",
            Target = "labapi-net48",
            RootNamespace = "HelloEPLab",
            References = new List<string>
            {
                "game:LabApi",
                "game:Assembly-CSharp",
                "game:CommandSystem.Core",
            },
        };
        project.Save(Path.Combine(directory, "project.eplabproj"));
        File.WriteAllText(Path.Combine(sourceDirectory, "你好.易"), HelloSource, new System.Text.UTF8Encoding(false));
    }

    public const string HelloSource = """
        .版本 2
        .扩展 CLR 1
        .扩展 LabAPI 1

        .引用命名空间 LabApi.Events.Arguments.PlayerEvents
        .引用命名空间 LabApi.Events.Handlers

        .LabAPI插件 主程序集
        .插件名称 “你好，EPLab！”
        .插件说明 “玩家加入时，在服务器日志里说你好。”
        .插件作者 “Your Name”
        .插件版本 “1.0.0”
        .订阅事件 PlayerEvents.Joined, 玩家加入

        .程序集 主程序集, , 公开

        .子程序 玩家加入, , 公开
        .参数 事件, PlayerJoinedEventArgs

        日志.信息（$“你好，{事件.Player.Nickname}！”）
        """;
}
