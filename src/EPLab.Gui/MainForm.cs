using System.Diagnostics;
using EPLab.Compiler;
using EPLab.Compiler.Build;

namespace EPLab.Gui;

public sealed class MainForm : Form
{
    private readonly Button newButton = new();
    private readonly Button openButton = new();
    private readonly Button saveButton = new();
    private readonly Button checkButton = new();
    private readonly Button buildButton = new();
    private readonly Button generatedButton = new();
    private readonly Button settingsButton = new();
    private readonly Label languageLabel = new();
    private readonly ComboBox languageBox = new();
    private readonly ListBox filesList = new();
    private readonly RichTextBox editor = new();
    private readonly ListBox diagnosticsList = new();
    private readonly TextBox outputBox = new();
    private readonly TabPage problemsTab = new();
    private readonly TabPage outputTab = new();
    private readonly Label statusLabel = new();
    private string? projectFile;
    private string? currentSourceFile;
    private CompilationResult? lastResult;
    private bool loadingSource;
    private bool suppressFileSelection;
    private SourceListItem? currentSourceItem;
    private string? projectName;

    public MainForm()
    {
        Text = "EPLab";
        Width = 1180;
        Height = 780;
        MinimumSize = new Size(820, 560);
        StartPosition = FormStartPosition.CenterScreen;
        AutoScaleMode = AutoScaleMode.Dpi;

        Panel topPanel = new() { Dock = DockStyle.Top, Height = 42 };
        FlowLayoutPanel buttons = new()
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(6, 5, 6, 4),
            WrapContents = false,
        };
        topPanel.Controls.Add(buttons);

        foreach (Button button in new[] { newButton, openButton, saveButton, checkButton, buildButton, generatedButton, settingsButton })
        {
            button.AutoSize = true;
            button.Height = 30;
            buttons.Controls.Add(button);
        }
        languageLabel.AutoSize = true;
        languageLabel.Margin = new Padding(10, 7, 3, 0);
        buttons.Controls.Add(languageLabel);
        languageBox.DropDownStyle = ComboBoxStyle.DropDownList;
        languageBox.Items.AddRange(new object[] { "中文", "English" });
        languageBox.SelectedIndex = 0;
        languageBox.Width = 92;
        buttons.Controls.Add(languageBox);

        SplitContainer vertical = new() { Dock = DockStyle.Fill, Orientation = Orientation.Horizontal, SplitterDistance = 520 };
        SplitContainer horizontal = new()
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Vertical,
            FixedPanel = FixedPanel.Panel1,
        };
        filesList.Dock = DockStyle.Fill;
        filesList.Font = new Font("Microsoft YaHei UI", 10);
        filesList.AccessibleName = "Source files";
        editor.Dock = DockStyle.Fill;
        editor.AcceptsTab = true;
        editor.WordWrap = false;
        editor.Font = new Font("Cascadia Mono", 11);
        editor.DetectUrls = false;
        editor.AccessibleName = "Source editor";
        horizontal.Panel1.Controls.Add(filesList);
        horizontal.Panel2.Controls.Add(editor);
        vertical.Panel1.Controls.Add(horizontal);

        TabControl bottomTabs = new() { Dock = DockStyle.Fill };
        diagnosticsList.Dock = DockStyle.Fill;
        diagnosticsList.Font = new Font("Cascadia Mono", 9);
        diagnosticsList.HorizontalScrollbar = true;
        diagnosticsList.AccessibleName = "Compiler problems";
        outputBox.Dock = DockStyle.Fill;
        outputBox.Multiline = true;
        outputBox.ReadOnly = true;
        outputBox.ScrollBars = ScrollBars.Both;
        outputBox.WordWrap = false;
        outputBox.Font = new Font("Cascadia Mono", 9);
        outputBox.AccessibleName = "Compiler output";
        problemsTab.Controls.Add(diagnosticsList);
        outputTab.Controls.Add(outputBox);
        bottomTabs.TabPages.Add(problemsTab);
        bottomTabs.TabPages.Add(outputTab);
        vertical.Panel2.Controls.Add(bottomTabs);

        statusLabel.Dock = DockStyle.Bottom;
        statusLabel.Height = 25;
        statusLabel.Padding = new Padding(6, 4, 6, 3);
        statusLabel.AccessibleName = "Status";
        statusLabel.TextChanged += (_, _) =>
        {
            statusLabel.AccessibleName = statusLabel.Text;
            statusLabel.AccessibleDescription = statusLabel.Text;
        };

        Controls.Add(vertical);
        Controls.Add(topPanel);
        Controls.Add(statusLabel);

        newButton.Click += (_, _) => CreateProject();
        openButton.Click += (_, _) => OpenProjectDialog();
        saveButton.Click += (_, _) => _ = SaveCurrentSource();
        checkButton.Click += async (_, _) => await CompileAsync(checkOnly: true);
        buildButton.Click += async (_, _) => await CompileAsync(checkOnly: false);
        generatedButton.Click += (_, _) => ShowGeneratedCSharp();
        settingsButton.Click += (_, _) => ShowProjectSettings();
        languageBox.SelectedIndexChanged += (_, _) => UpdateLanguage();
        filesList.SelectedIndexChanged += (_, _) => LoadSelectedSource();
        diagnosticsList.DoubleClick += (_, _) => JumpToDiagnostic();
        FormClosing += (_, args) =>
        {
            if (!ConfirmDiscard())
                args.Cancel = true;
        };
        editor.TextChanged += (_, _) =>
        {
            if (!loadingSource)
                saveButton.Enabled = currentSourceFile is not null;
        };
        Shown += (_, _) =>
        {
            if (horizontal.Width > 300)
                horizontal.SplitterDistance = Math.Min(225, horizontal.Width / 3);
        };

        UpdateLanguage();
        SetProjectButtons(false);
    }

    private bool Chinese => languageBox.SelectedIndex == 0;

    private void UpdateLanguage()
    {
        newButton.Text = Chinese ? "新建" : "New";
        openButton.Text = Chinese ? "打开" : "Open";
        saveButton.Text = Chinese ? "保存" : "Save";
        checkButton.Text = Chinese ? "检查" : "Check";
        buildButton.Text = Chinese ? "编译 DLL" : "Build DLL";
        generatedButton.Text = Chinese ? "查看生成的 C#" : "View generated C#";
        settingsButton.Text = Chinese ? "项目设置" : "Project settings";
        languageLabel.Text = Chinese ? "语言：" : "Language:";
        problemsTab.Text = Chinese ? "问题" : "Problems";
        outputTab.Text = Chinese ? "输出" : "Output";
        languageBox.AccessibleName = Chinese ? "界面语言" : "Interface language";
        filesList.AccessibleName = Chinese ? "源代码文件" : "Source files";
        editor.AccessibleName = Chinese ? "源代码编辑器" : "Source editor";
        diagnosticsList.AccessibleName = Chinese ? "编译问题" : "Compiler problems";
        outputBox.AccessibleName = Chinese ? "编译输出" : "Compiler output";
        statusLabel.AccessibleName = Chinese ? "状态" : "Status";
        Text = projectName is null
            ? (Chinese ? "EPLab — 易风格 LabAPI 编译器" : "EPLab — Easy-style LabAPI compiler")
            : $"EPLab — {projectName}";
        statusLabel.Text = projectFile is null
            ? (Chinese ? "先新建或打开一个项目。" : "Create or open a project first.")
            : (Chinese ? "项目已打开。按“检查”找问题，按“编译 DLL”做插件。" : "Project opened. Check for problems, then build the DLL.");
        RefreshDiagnosticText();
    }

    private void CreateProject()
    {
        if (!ConfirmDiscard())
            return;
        using FolderBrowserDialog dialog = new()
        {
            Description = Chinese ? "选择一个空文件夹来放新项目" : "Choose an empty folder for the new project",
            UseDescriptionForTitle = true,
        };
        if (dialog.ShowDialog(this) != DialogResult.OK)
            return;
        try
        {
            ProjectTemplates.CreateHelloProject(dialog.SelectedPath, chinese: Chinese);
            LoadProject(Path.Combine(dialog.SelectedPath, "project.eplabproj"), confirmDiscard: false);
        }
        catch (Exception exception)
        {
            MessageBox.Show(this, exception.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void OpenProjectDialog()
    {
        using OpenFileDialog dialog = new()
        {
            Filter = Chinese
                ? "EPLab 项目 (*.eplabproj)|*.eplabproj|所有文件 (*.*)|*.*"
                : "EPLab project (*.eplabproj)|*.eplabproj|All files (*.*)|*.*",
            Title = Chinese ? "打开 EPLab 项目" : "Open an EPLab project",
        };
        if (dialog.ShowDialog(this) != DialogResult.OK)
            return;
        try
        {
            LoadProject(dialog.FileName);
        }
        catch (Exception exception)
        {
            MessageBox.Show(this, exception.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void LoadProject(string path, bool confirmDiscard = true)
    {
        if (confirmDiscard && !ConfirmDiscard())
            return;
        EPLabProject project = EPLabProject.Load(path);
        projectFile = Path.GetFullPath(path);
        string root = Path.GetDirectoryName(projectFile)!;
        string[] files = project.SourceDirectories
            .Select(directory => Path.Combine(root, directory))
            .Where(Directory.Exists)
            .SelectMany(directory => Directory.EnumerateFiles(directory, "*.*", SearchOption.AllDirectories))
            .Where(file => file.EndsWith(".易", StringComparison.OrdinalIgnoreCase) || file.EndsWith(".eplab", StringComparison.OrdinalIgnoreCase))
            .OrderBy(file => file, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        suppressFileSelection = true;
        filesList.Items.Clear();
        foreach (string file in files)
            filesList.Items.Add(new SourceListItem(file, Path.GetRelativePath(root, file)));
        currentSourceFile = null;
        currentSourceItem = null;
        loadingSource = true;
        editor.Clear();
        editor.Modified = false;
        loadingSource = false;
        suppressFileSelection = false;
        projectName = project.Name;
        Text = $"EPLab — {projectName}";
        lastResult = null;
        diagnosticsList.Items.Clear();
        outputBox.Clear();
        SetProjectButtons(true);
        if (filesList.Items.Count > 0)
            filesList.SelectedIndex = 0;
        statusLabel.Text = Chinese ? "项目已打开。按“检查”找问题，按“编译 DLL”做插件。" : "Project opened. Check for problems, then build the DLL.";
    }

    private void LoadSelectedSource()
    {
        if (suppressFileSelection)
            return;
        if (filesList.SelectedItem is not SourceListItem item)
            return;
        if (string.Equals(currentSourceFile, item.Path, StringComparison.OrdinalIgnoreCase))
            return;
        if (!ConfirmDiscard())
        {
            suppressFileSelection = true;
            filesList.SelectedItem = currentSourceItem;
            suppressFileSelection = false;
            return;
        }
        loadingSource = true;
        try
        {
            string source = File.ReadAllText(item.Path);
            editor.Text = source;
            currentSourceFile = item.Path;
            currentSourceItem = item;
            editor.Modified = false;
            saveButton.Enabled = false;
        }
        catch (Exception exception)
        {
            suppressFileSelection = true;
            filesList.SelectedItem = currentSourceItem;
            suppressFileSelection = false;
            MessageBox.Show(this, exception.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            loadingSource = false;
        }
    }

    private bool SaveCurrentSource()
    {
        if (currentSourceFile is null || !editor.Modified)
            return true;
        try
        {
            File.WriteAllText(currentSourceFile, editor.Text, new System.Text.UTF8Encoding(false));
            editor.Modified = false;
            saveButton.Enabled = false;
            statusLabel.Text = Chinese ? "已保存。" : "Saved.";
            return true;
        }
        catch (Exception exception)
        {
            MessageBox.Show(this, exception.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
            return false;
        }
    }

    private bool ConfirmDiscard()
    {
        if (!editor.Modified)
            return true;
        DialogResult result = MessageBox.Show(
            this,
            Chinese ? "当前文件改过但还没保存。现在保存吗？" : "The current file has unsaved changes. Save it now?",
            Text,
            MessageBoxButtons.YesNoCancel,
            MessageBoxIcon.Question);
        if (result == DialogResult.Cancel)
            return false;
        if (result == DialogResult.Yes)
            return SaveCurrentSource();
        return true;
    }

    private async Task CompileAsync(bool checkOnly)
    {
        if (projectFile is null)
            return;
        if (!SaveCurrentSource())
            return;
        SetBusy(true);
        diagnosticsList.Items.Clear();
        outputBox.Clear();
        statusLabel.Text = Chinese ? "正在认真检查……" : "Checking carefully…";
        try
        {
            lastResult = await new ProjectCompiler().CompileAsync(
                projectFile,
                new CompilerOptions(ChineseDiagnostics: Chinese, CheckOnly: checkOnly));
            foreach (Diagnostic diagnostic in lastResult.Diagnostics)
                diagnosticsList.Items.Add(new DiagnosticListItem(diagnostic, Chinese));
            outputBox.Text = lastResult.BuildLog;
            statusLabel.Text = lastResult.Success
                ? checkOnly
                    ? (Chinese ? "检查通过！现在可以编译 DLL。" : "Check passed! You can build the DLL now.")
                    : (Chinese ? $"成功！DLL：{lastResult.AssemblyPath}" : $"Success! DLL: {lastResult.AssemblyPath}")
                : (Chinese ? "没有成功。双击下面的问题可以跳到那一行。" : "Not successful. Double-click a problem to jump to its line.");
        }
        catch (Exception exception)
        {
            outputBox.Text = exception.ToString();
            statusLabel.Text = Chinese ? "编译器遇到了意外问题。" : "The compiler hit an unexpected problem.";
        }
        finally
        {
            SetBusy(false);
        }
    }

    private void ShowGeneratedCSharp()
    {
        string? path = lastResult?.GeneratedCSharpPath;
        if (path is null || !File.Exists(path))
        {
            MessageBox.Show(this, Chinese ? "请先按一次“检查”或“编译 DLL”。" : "Run Check or Build DLL first.", Text);
            return;
        }
        Form viewer = new()
        {
            Text = Chinese ? "生成的 C#" : "Generated C#",
            Width = 1000,
            Height = 700,
            StartPosition = FormStartPosition.CenterParent,
        };
        RichTextBox text = new()
        {
            Dock = DockStyle.Fill,
            ReadOnly = true,
            WordWrap = false,
            Font = new Font("Cascadia Mono", 10),
            Text = File.ReadAllText(path),
        };
        viewer.Controls.Add(text);
        viewer.Show(this);
    }

    private void ShowProjectSettings()
    {
        if (projectFile is null)
            return;
        try
        {
            EPLabProject project = EPLabProject.Load(projectFile);
            using ProjectSettingsForm dialog = new(project, Chinese);
            if (dialog.ShowDialog(this) != DialogResult.OK)
                return;
            project.ManagedDirectory = EmptyToNull(dialog.ManagedDirectory);
            project.GlobalDependenciesDirectory = EmptyToNull(dialog.DependenciesDirectory);
            project.Save(projectFile);
            statusLabel.Text = Chinese ? "项目设置已保存。" : "Project settings saved.";
        }
        catch (Exception exception)
        {
            MessageBox.Show(this, exception.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private static string? EmptyToNull(string value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private void JumpToDiagnostic()
    {
        if (diagnosticsList.SelectedItem is not DiagnosticListItem item)
            return;
        string path = Path.GetFullPath(item.Diagnostic.Span.FilePath);
        SourceListItem? source = filesList.Items.OfType<SourceListItem>().FirstOrDefault(candidate => string.Equals(Path.GetFullPath(candidate.Path), path, StringComparison.OrdinalIgnoreCase));
        if (source is null)
            return;
        filesList.SelectedItem = source;
        int line = Math.Max(1, item.Diagnostic.Span.Line);
        int index = 0;
        for (int currentLine = 1; currentLine < line && index < editor.TextLength; currentLine++)
        {
            int next = editor.Text.IndexOf('\n', index);
            index = next < 0 ? editor.TextLength : next + 1;
        }
        editor.Select(index, Math.Min(Math.Max(1, item.Diagnostic.Span.Length), editor.TextLength - index));
        editor.ScrollToCaret();
        editor.Focus();
    }

    private void RefreshDiagnosticText()
    {
        DiagnosticListItem[] items = diagnosticsList.Items.OfType<DiagnosticListItem>().ToArray();
        diagnosticsList.Items.Clear();
        foreach (DiagnosticListItem item in items)
            diagnosticsList.Items.Add(new DiagnosticListItem(item.Diagnostic, Chinese));
    }

    private void SetProjectButtons(bool enabled)
    {
        saveButton.Enabled = false;
        checkButton.Enabled = enabled;
        buildButton.Enabled = enabled;
        generatedButton.Enabled = enabled;
        settingsButton.Enabled = enabled;
    }

    private void SetBusy(bool busy)
    {
        newButton.Enabled = !busy;
        openButton.Enabled = !busy;
        checkButton.Enabled = !busy && projectFile is not null;
        buildButton.Enabled = !busy && projectFile is not null;
        generatedButton.Enabled = !busy && projectFile is not null;
        settingsButton.Enabled = !busy && projectFile is not null;
        saveButton.Enabled = !busy && currentSourceFile is not null && editor.Modified;
        filesList.Enabled = !busy;
        editor.ReadOnly = busy;
        languageBox.Enabled = !busy;
        UseWaitCursor = busy;
    }

    private sealed record SourceListItem(string Path, string Label)
    {
        public override string ToString() => Label;
    }

    private sealed record DiagnosticListItem(Diagnostic Diagnostic, bool Chinese)
    {
        public override string ToString() => Diagnostic.Format(Chinese);
    }

    private sealed class ProjectSettingsForm : Form
    {
        private readonly TextBox managed = new() { Dock = DockStyle.Fill };
        private readonly TextBox dependencies = new() { Dock = DockStyle.Fill };

        public ProjectSettingsForm(EPLabProject project, bool chinese)
        {
            Text = chinese ? "项目设置" : "Project settings";
            Width = 850;
            Height = 240;
            MinimumSize = new Size(620, 220);
            StartPosition = FormStartPosition.CenterParent;
            AutoScaleMode = AutoScaleMode.Dpi;
            managed.Text = project.ManagedDirectory ?? string.Empty;
            dependencies.Text = project.GlobalDependenciesDirectory ?? string.Empty;

            TableLayoutPanel table = new()
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(12),
                ColumnCount = 3,
                RowCount = 3,
            };
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 260));
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 90));
            table.RowStyles.Add(new RowStyle(SizeType.Absolute, 52));
            table.RowStyles.Add(new RowStyle(SizeType.Absolute, 52));
            table.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            table.Controls.Add(new Label
            {
                Text = chinese ? "游戏 Managed 文件夹：" : "Game Managed folder:",
                AutoSize = true,
                Anchor = AnchorStyles.Left,
            }, 0, 0);
            table.Controls.Add(managed, 1, 0);
            Button browseManaged = new() { Text = chinese ? "选择…" : "Browse…", AutoSize = true, Anchor = AnchorStyles.Left };
            browseManaged.Click += (_, _) => Browse(managed, chinese ? "选择 SCPSL_Data\\Managed" : "Choose SCPSL_Data\\Managed");
            table.Controls.Add(browseManaged, 2, 0);

            table.Controls.Add(new Label
            {
                Text = chinese ? "LabAPI 全局依赖文件夹：" : "LabAPI global dependencies:",
                AutoSize = true,
                Anchor = AnchorStyles.Left,
            }, 0, 1);
            table.Controls.Add(dependencies, 1, 1);
            Button browseDependencies = new() { Text = chinese ? "选择…" : "Browse…", AutoSize = true, Anchor = AnchorStyles.Left };
            browseDependencies.Click += (_, _) => Browse(dependencies, chinese ? "选择 dependencies\\global" : "Choose dependencies\\global");
            table.Controls.Add(browseDependencies, 2, 1);

            FlowLayoutPanel actions = new() { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft };
            Button ok = new() { Text = chinese ? "保存" : "Save", DialogResult = DialogResult.OK, AutoSize = true };
            Button cancel = new() { Text = chinese ? "取消" : "Cancel", DialogResult = DialogResult.Cancel, AutoSize = true };
            actions.Controls.Add(ok);
            actions.Controls.Add(cancel);
            table.SetColumnSpan(actions, 3);
            table.Controls.Add(actions, 0, 2);
            Controls.Add(table);
            AcceptButton = ok;
            CancelButton = cancel;
        }

        public string ManagedDirectory => managed.Text;
        public string DependenciesDirectory => dependencies.Text;

        private void Browse(TextBox target, string description)
        {
            using FolderBrowserDialog dialog = new() { Description = description, UseDescriptionForTitle = true };
            if (Directory.Exists(target.Text)) dialog.InitialDirectory = target.Text;
            if (dialog.ShowDialog(this) == DialogResult.OK) target.Text = dialog.SelectedPath;
        }
    }
}
