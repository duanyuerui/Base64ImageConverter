using System.Drawing.Imaging;

namespace Base64ToImage;

public partial class Form1 : Form
{
    private readonly TextBox txtBase64;
    private readonly TextBox txtSavePath;
    private readonly Button btnBrowse;
    private readonly RadioButton rbWhiteBg;
    private readonly RadioButton rbTransparentBg;
    private readonly Button btnConvert;
    private readonly PictureBox picPreview;
    private readonly Label lblStatus;

    public Form1()
    {
        Text = "Base64 转图片";
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(600, 720);
        Padding = new Padding(12);
        Font = new Font("Microsoft YaHei", 10);

        // ---- Base64 输入 ----
        var base64HeaderPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 32,
            ColumnCount = 2,
            RowCount = 1,
        };
        base64HeaderPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        base64HeaderPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100));

        var lblBase64 = new Label
        {
            Text = "Base64 字符串：",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
        };

        var btnImport = new Button
        {
            Text = "从文件导入",
            Dock = DockStyle.Fill,
            FlatStyle = FlatStyle.Flat,
        };
        btnImport.Click += BtnImport_Click;

        base64HeaderPanel.Controls.Add(lblBase64, 0, 0);
        base64HeaderPanel.Controls.Add(btnImport, 1, 0);

        txtBase64 = new TextBox
        {
            Multiline = true,
            ScrollBars = ScrollBars.Both,
            Dock = DockStyle.Top,
            Height = 300,
            Font = new Font("Consolas", 10),
            AcceptsReturn = true,
            WordWrap = false,
        };

        // ---- 保存路径 ----
        var lblPath = new Label
        {
            Text = "保存路径：",
            Dock = DockStyle.Top,
            Height = 28,
            Margin = new Padding(0, 8, 0, 0),
        };

        var pathPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 36,
            ColumnCount = 2,
            RowCount = 1,
        };
        pathPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        pathPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 80));

        txtSavePath = new TextBox
        {
            Dock = DockStyle.Fill,
            Text = Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
            Font = new Font("Consolas", 10),
        };

        btnBrowse = new Button
        {
            Text = "浏览...",
            Dock = DockStyle.Fill,
            FlatStyle = FlatStyle.Flat,
        };
        btnBrowse.Click += BtnBrowse_Click;

        pathPanel.Controls.Add(txtSavePath, 0, 0);
        pathPanel.Controls.Add(btnBrowse, 1, 0);

        // ---- 背景选择 ----
        var lblBg = new Label
        {
            Text = "背景：",
            Dock = DockStyle.Top,
            Height = 28,
            Margin = new Padding(0, 4, 0, 0),
        };

        var bgPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 32,
            Margin = new Padding(0),
        };

        rbWhiteBg = new RadioButton
        {
            Text = "白色背景",
            Checked = true,
            AutoSize = true,
        };
        rbTransparentBg = new RadioButton
        {
            Text = "透明背景（PNG）",
            AutoSize = true,
        };

        bgPanel.Controls.Add(rbWhiteBg);
        bgPanel.Controls.Add(rbTransparentBg);

        // ---- 转换按钮 ----
        btnConvert = new Button
        {
            Text = "转换",
            Dock = DockStyle.Top,
            Height = 40,
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.DodgerBlue,
            ForeColor = Color.White,
            Font = new Font("Microsoft YaHei", 12, FontStyle.Bold),
            Cursor = Cursors.Hand,
            Margin = new Padding(0, 8, 0, 4),
        };
        btnConvert.Click += BtnConvert_Click;

        // ---- 预览 ----
        var lblPreview = new Label
        {
            Text = "预览：",
            Dock = DockStyle.Top,
            Height = 28,
        };

        var previewPanel = new Panel
        {
            Dock = DockStyle.Fill,
            BorderStyle = BorderStyle.FixedSingle,
            BackColor = Color.White,
        };

        picPreview = new PictureBox
        {
            Dock = DockStyle.Fill,
            SizeMode = PictureBoxSizeMode.Zoom,
            BackColor = Color.White,
        };
        previewPanel.Controls.Add(picPreview);

        // ---- 状态栏 ----
        lblStatus = new Label
        {
            Dock = DockStyle.Bottom,
            Height = 28,
            ForeColor = Color.Gray,
            TextAlign = ContentAlignment.MiddleLeft,
        };

        // ---- 控件顺序（从下往上 Dock） ----
        Controls.Add(previewPanel);
        Controls.Add(lblPreview);
        Controls.Add(btnConvert);
        Controls.Add(bgPanel);
        Controls.Add(lblBg);
        Controls.Add(pathPanel);
        Controls.Add(lblPath);
        Controls.Add(txtBase64);
        Controls.Add(base64HeaderPanel);
        Controls.Add(lblStatus);
    }

    private void BtnBrowse_Click(object? sender, EventArgs e)
    {
        using var dialog = new FolderBrowserDialog();
        dialog.SelectedPath = txtSavePath.Text;
        if (dialog.ShowDialog() == DialogResult.OK)
        {
            txtSavePath.Text = dialog.SelectedPath;
        }
    }

    private void BtnImport_Click(object? sender, EventArgs e)
    {
        using var dialog = new OpenFileDialog
        {
            Title = "选择 Base64 文本文件",
            Filter = "文本文件 (*.txt)|*.txt|所有文件 (*.*)|*.*",
            RestoreDirectory = true,
        };
        if (dialog.ShowDialog() != DialogResult.OK) return;

        try
        {
            var content = File.ReadAllText(dialog.FileName);
            txtBase64.Text = content;
            ShowStatus($"已从文件导入：{Path.GetFileName(dialog.FileName)}（{content.Length} 字符）", Color.DodgerBlue);
        }
        catch (Exception ex)
        {
            ShowStatus($"读取文件失败：{ex.Message}", Color.Red);
        }
    }

    private void BtnConvert_Click(object? sender, EventArgs e)
    {
        var base64Input = txtBase64.Text.Trim();
        if (string.IsNullOrEmpty(base64Input))
        {
            ShowStatus("请输入 Base64 字符串。", Color.Red);
            return;
        }

        var saveDir = txtSavePath.Text.Trim();
        if (string.IsNullOrEmpty(saveDir) || !Directory.Exists(saveDir))
        {
            ShowStatus("保存路径无效，请选择有效目录。", Color.Red);
            return;
        }

        btnConvert.Enabled = false;
        btnConvert.Text = "转换中...";
        lblStatus.Text = "";

        try
        {
            var useWhiteBg = rbWhiteBg.Checked;
            var fileName = $"base64_image_{DateTime.Now:yyyyMMdd_HHmmss}.png";
            var savePath = Path.Combine(saveDir, fileName);

            // 使用新工具类直接保存到本地
            DATBase64Saver.Save(base64Input, savePath, useWhiteBg);

            // 预览：从保存的文件读取
            using var fs = new FileStream(savePath, FileMode.Open, FileAccess.Read);
            var oldPreview = picPreview.Image;
            picPreview.Image = new Bitmap(fs);
            oldPreview?.Dispose();

            ShowStatus($"转换成功！已保存至：{savePath}", Color.Green);
        }
        catch (FormatException)
        {
            ShowStatus("Base64 格式无效，请检查输入。", Color.Red);
            picPreview.Image = null;
        }
        catch (ArgumentException)
        {
            ShowStatus("无法识别的图片格式，请确认 Base64 是有效的图片数据。", Color.Red);
            picPreview.Image = null;
        }
        catch (Exception ex)
        {
            ShowStatus($"转换失败：{ex.Message}", Color.Red);
            picPreview.Image = null;
        }
        finally
        {
            btnConvert.Enabled = true;
            btnConvert.Text = "转换";
        }
    }

    private void ShowStatus(string message, Color color)
    {
        lblStatus.Text = message;
        lblStatus.ForeColor = color;
    }
}
