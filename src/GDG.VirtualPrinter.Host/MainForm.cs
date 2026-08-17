namespace GDG.VirtualPrinter.Host;

using GDG.VirtualPrinter.Core;
using System.Diagnostics;
using System.Security.Principal;
using System.Windows.Forms;

public sealed class MainForm : Form
{
    private readonly TextBox txtExecutable = new();
    private readonly TextBox txtArguments = new();
    private readonly TextBox txtWorkingDirectory = new();
    private readonly TextBox txtJobsDirectory = new();

    private readonly RadioButton rbCurrentUser = new();
    private readonly RadioButton rbSpecificAccount = new();
    private readonly TextBox txtAccount = new();
    private readonly TextBox txtPassword = new();
    private readonly Button btnVerifyCredentials = new();

    private readonly CheckBox chkKeepOxps = new();
    private readonly RadioButton rbOutputOxps = new();
    private readonly RadioButton rbOutputXps = new();
    private readonly RadioButton rbOutputBoth = new();

    private readonly TextBox txtRedMonPort = new();
    private readonly TextBox txtRedMonPrinter = new();
    private readonly TextBox txtRedMonOutputPrinter = new();

    private readonly Label lblStatus = new();

    private BridgeSettings settings = new();

    public MainForm()
    {
        Text = "GDG Virtual Printer - Configurazione";
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(760, 650);
        Width = 860;
        Height = 720;

        BuildUi();
        LoadSettings();
    }

    private void BuildUi()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 6,
            Padding = new Padding(12),
            AutoScroll = true
        };

        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        root.Controls.Add(BuildProgramGroup());
        root.Controls.Add(BuildAccountGroup());
        root.Controls.Add(BuildRedMonGroup());
        root.Controls.Add(BuildOptionsGroup());

        lblStatus.AutoSize = true;
        lblStatus.Padding = new Padding(4);
        root.Controls.Add(lblStatus);

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            AutoSize = true
        };

        var btnClose = new Button { Text = "Chiudi", AutoSize = true };
        btnClose.Click += (_, _) => Close();

        var btnSave = new Button { Text = "Salva", AutoSize = true };
        btnSave.Click += (_, _) => SaveSettings();

        var btnTest = new Button { Text = "Test applicazione", AutoSize = true };
        btnTest.Click += (_, _) => TestApplication();

        buttons.Controls.Add(btnClose);
        buttons.Controls.Add(btnSave);
        buttons.Controls.Add(btnTest);
        root.Controls.Add(buttons);

        Controls.Add(root);
    }

    private Control BuildProgramGroup()
    {
        var group = CreateGroup("Programma da eseguire");
        var table = CreateTable();

        AddBrowseRow(table, 0, "Eseguibile", txtExecutable, BrowseExecutable);
        AddTextRow(table, 1, "Argomenti", txtArguments);
        AddBrowseRow(table, 2, "Directory di lavoro", txtWorkingDirectory, BrowseWorkingDirectory);
        AddBrowseRow(table, 3, "Cartella OXPS", txtJobsDirectory, BrowseJobsDirectory);

        group.Controls.Add(table);
        return group;
    }

    private Control BuildAccountGroup()
    {
        var group = CreateGroup("Account di esecuzione");
        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            ColumnCount = 3
        };

        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 160));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        rbCurrentUser.Text = "Utente corrente";
        rbCurrentUser.AutoSize = true;
        rbCurrentUser.CheckedChanged += (_, _) => UpdateAccountControls();

        rbSpecificAccount.Text = "Account specifico";
        rbSpecificAccount.AutoSize = true;
        rbSpecificAccount.CheckedChanged += (_, _) => UpdateAccountControls();

        panel.Controls.Add(rbCurrentUser, 0, 0);
        panel.SetColumnSpan(rbCurrentUser, 3);
        panel.Controls.Add(rbSpecificAccount, 0, 1);
        panel.SetColumnSpan(rbSpecificAccount, 3);

        panel.Controls.Add(new Label
        {
            Text = "Account",
            AutoSize = true,
            Anchor = AnchorStyles.Left
        }, 0, 2);

        txtAccount.Dock = DockStyle.Fill;
        panel.Controls.Add(txtAccount, 1, 2);
        panel.SetColumnSpan(txtAccount, 2);

        panel.Controls.Add(new Label
        {
            Text = "Password",
            AutoSize = true,
            Anchor = AnchorStyles.Left
        }, 0, 3);

        txtPassword.Dock = DockStyle.Fill;
        txtPassword.UseSystemPasswordChar = true;
        panel.Controls.Add(txtPassword, 1, 3);

        btnVerifyCredentials.Text = "Verifica credenziali";
        btnVerifyCredentials.AutoSize = true;
        btnVerifyCredentials.Click += (_, _) => VerifyCredentials();
        panel.Controls.Add(btnVerifyCredentials, 2, 3);

        group.Controls.Add(panel);
        return group;
    }

    private Control BuildRedMonGroup()
    {
        var group = CreateGroup("Compatibilità RedMon");
        var table = CreateTable();

        AddTextRow(table, 0, "REDMON_PORT", txtRedMonPort);
        AddTextRow(table, 1, "REDMON_PRINTER", txtRedMonPrinter);
        AddTextRow(table, 2, "REDMON_OUTPUTPRINTER", txtRedMonOutputPrinter);

        group.Controls.Add(table);
        return group;
    }

    private Control BuildOptionsGroup()
    {
        var group = CreateGroup("Formato consegnato al processor");

        var panel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            Padding = new Padding(8)
        };

        rbOutputOxps.Text = "OXPS originale";
        rbOutputOxps.AutoSize = true;

        rbOutputXps.Text = "XPS";
        rbOutputXps.AutoSize = true;

        rbOutputBoth.Text = "Entrambi";
        rbOutputBoth.AutoSize = true;

        chkKeepOxps.Text = "Mantieni il file OXPS quando il formato consegnato è XPS";
        chkKeepOxps.AutoSize = true;
        chkKeepOxps.Padding = new Padding(0, 8, 0, 0);

        panel.Controls.Add(rbOutputOxps);
        panel.Controls.Add(rbOutputXps);
        panel.Controls.Add(rbOutputBoth);
        panel.Controls.Add(chkKeepOxps);

        group.Controls.Add(panel);
        return group;
    }

    private static GroupBox CreateGroup(string title)
        => new()
        {
            Text = title,
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Padding = new Padding(10)
        };

    private static TableLayoutPanel CreateTable()
    {
        var table = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            ColumnCount = 3
        };

        table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 160));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        return table;
    }

    private static void AddTextRow(
        TableLayoutPanel table,
        int row,
        string label,
        TextBox textBox)
    {
        table.Controls.Add(new Label
        {
            Text = label,
            AutoSize = true,
            Anchor = AnchorStyles.Left
        }, 0, row);

        textBox.Dock = DockStyle.Fill;
        table.Controls.Add(textBox, 1, row);
        table.SetColumnSpan(textBox, 2);
    }

    private static void AddBrowseRow(
        TableLayoutPanel table,
        int row,
        string label,
        TextBox textBox,
        EventHandler click)
    {
        table.Controls.Add(new Label
        {
            Text = label,
            AutoSize = true,
            Anchor = AnchorStyles.Left
        }, 0, row);

        textBox.Dock = DockStyle.Fill;
        table.Controls.Add(textBox, 1, row);

        var button = new Button { Text = "Sfoglia...", AutoSize = true };
        button.Click += click;
        table.Controls.Add(button, 2, row);
    }

    private void LoadSettings()
    {
        BridgePaths.EnsureProgramDataFolders();
        settings = BridgeSettings.LoadOrCreate();

        txtExecutable.Text = settings.ExecutablePath;
        txtArguments.Text = settings.Arguments;
        txtWorkingDirectory.Text = settings.WorkingDirectory;
        txtJobsDirectory.Text = settings.JobsDirectory;
        chkKeepOxps.Checked = settings.KeepOxps;
        rbOutputOxps.Checked = settings.OutputFormat == ProcessorOutputFormat.Oxps;
        rbOutputXps.Checked = settings.OutputFormat == ProcessorOutputFormat.Xps;
        rbOutputBoth.Checked = settings.OutputFormat == ProcessorOutputFormat.Both;

        rbCurrentUser.Checked = settings.RunAsMode == RunAsMode.CurrentUser;
        rbSpecificAccount.Checked = settings.RunAsMode == RunAsMode.SpecificAccount;
        txtAccount.Text = settings.RunAsUser;

        txtRedMonPort.Text = settings.RedMonPort;
        txtRedMonPrinter.Text = settings.RedMonPrinter;
        txtRedMonOutputPrinter.Text = settings.RedMonOutputPrinter;

        if (settings.RunAsMode == RunAsMode.SpecificAccount)
        {
            var credential = CredentialManager.Read(settings.CredentialTarget);
            if (credential is not null)
                txtPassword.Text = credential.Value.Password;
        }

        UpdateAccountControls();
        SetStatus("Configurazione caricata.");
    }

    private void SaveSettings()
    {
        try
        {
            ValidateForm();

            settings.ExecutablePath = txtExecutable.Text.Trim();
            settings.Arguments = txtArguments.Text;
            settings.WorkingDirectory = txtWorkingDirectory.Text.Trim();
            settings.JobsDirectory = txtJobsDirectory.Text.Trim();
            settings.KeepOxps = chkKeepOxps.Checked;
            settings.OutputFormat = rbOutputOxps.Checked
                ? ProcessorOutputFormat.Oxps
                : rbOutputBoth.Checked
                    ? ProcessorOutputFormat.Both
                    : ProcessorOutputFormat.Xps;

            settings.RunAsMode = rbSpecificAccount.Checked
                ? RunAsMode.SpecificAccount
                : RunAsMode.CurrentUser;
            settings.RunAsUser = txtAccount.Text.Trim();

            settings.RedMonPort = txtRedMonPort.Text.Trim();
            settings.RedMonPrinter = txtRedMonPrinter.Text.Trim();
            settings.RedMonOutputPrinter = txtRedMonOutputPrinter.Text.Trim();

            if (settings.RunAsMode == RunAsMode.SpecificAccount)
            {
                AccountValidator.Validate(settings.RunAsUser, txtPassword.Text);

                CredentialManager.Save(
                    settings.CredentialTarget,
                    settings.RunAsUser,
                    txtPassword.Text);

                JobFolderSecurity.GrantModify(
                    settings.JobsDirectory,
                    settings.RunAsUser);
            }
            else
            {
                CredentialManager.Delete(settings.CredentialTarget);
            }

            settings.Save();
            SetStatus("Configurazione salvata.");
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                this,
                ex.Message,
                "Errore",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }

    private void VerifyCredentials()
    {
        try
        {
            AccountValidator.Validate(txtAccount.Text.Trim(), txtPassword.Text);
            SetStatus("Credenziali valide.");

            MessageBox.Show(
                this,
                "Le credenziali sono valide.",
                "GDG Virtual Printer",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                this,
                ex.Message,
                "Credenziali non valide",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }

    private void TestApplication()
    {
        try
        {
            ValidateForm();

            string tempOxps = Path.Combine(
                Path.GetTempPath(),
                "GDG_VirtualPrinter_Test_" + Guid.NewGuid().ToString("N") + ".oxps");

            File.WriteAllBytes(tempOxps, Array.Empty<byte>());

            string? tempXps = null;
            string tempJob = tempOxps;

            if (rbOutputXps.Checked || rbOutputBoth.Checked)
            {
                // The UI test does not synthesize a valid XPS package; it only tests
                // process launch/environment. Real conversion is exercised by a print job.
                tempXps = Path.ChangeExtension(tempOxps, ".xps");
                File.WriteAllBytes(tempXps, Array.Empty<byte>());

                if (rbOutputXps.Checked)
                    tempJob = tempXps;
            }

            var psi = new ProcessStartInfo
            {
                FileName = txtExecutable.Text.Trim(),
                UseShellExecute = false,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden,
                WorkingDirectory = string.IsNullOrWhiteSpace(txtWorkingDirectory.Text)
                    ? Path.GetDirectoryName(txtExecutable.Text.Trim()) ?? string.Empty
                    : txtWorkingDirectory.Text.Trim(),
                LoadUserProfile = true
            };

            if (!string.IsNullOrWhiteSpace(txtArguments.Text))
                psi.Arguments = txtArguments.Text;

            string runAsUser = WindowsIdentity.GetCurrent()?.Name ?? Environment.UserName;

            if (rbSpecificAccount.Checked)
            {
                AccountValidator.Validate(txtAccount.Text.Trim(), txtPassword.Text);
                AccountValidator.ParseAccount(
                    txtAccount.Text.Trim(),
                    out string? domain,
                    out string user);

                psi.UserName = user;
                psi.Domain = domain ?? string.Empty;
                psi.PasswordInClearText = txtPassword.Text;
                runAsUser = txtAccount.Text.Trim();
            }

            psi.Environment["REDMON_PORT"] = txtRedMonPort.Text.Trim();
            psi.Environment["REDMON_JOB"] = "TEST";
            psi.Environment["REDMON_PRINTER"] = txtRedMonPrinter.Text.Trim();
            psi.Environment["REDMON_OUTPUTPRINTER"] = txtRedMonOutputPrinter.Text.Trim();
            psi.Environment["REDMON_MACHINE"] = Environment.MachineName;
            psi.Environment["REDMON_USER"] = WindowsIdentity.GetCurrent()?.Name ?? Environment.UserName;
            psi.Environment["REDMON_DOCNAME"] = "GDG Virtual Printer Test";
            psi.Environment["REDMON_BASENAME"] = "GDG Virtual Printer Test";
            psi.Environment["REDMON_FILENAME"] = tempJob;
            psi.Environment["REDMON_SESSIONID"] = Process.GetCurrentProcess().SessionId.ToString();
            psi.Environment["GDG_RUNAS_USER"] = runAsUser;
            psi.Environment["GDG_SOURCE_APP"] = "GDG.VirtualPrinter.Host";
            psi.Environment["GDG_WORKFLOW_SESSION_ID"] = "TEST";
            psi.Environment["GDG_SPOOLER_JOB_ID"] = "TEST";
            psi.Environment["GDG_RDS_SESSION_ID"] = Process.GetCurrentProcess().SessionId.ToString();
            psi.Environment["GDG_RDS_SESSION_NAME"] = string.Empty;
            psi.Environment["GDG_RDS_CLIENT_NAME"] = string.Empty;
            psi.Environment["GDG_IS_REMOTE_SESSION"] = string.Empty;
            psi.Environment["GDG_SPOOLER_RESOLUTION"] = "TEST";
            psi.Environment["GDG_RDS_RESOLUTION"] = "TEST";
            psi.Environment["GDG_OXPS_FILENAME"] = tempOxps;
            psi.Environment["GDG_XPS_FILENAME"] = tempXps ?? string.Empty;
            psi.Environment["GDG_PROCESSOR_FILENAME"] = tempJob;
            psi.Environment["GDG_OUTPUT_FORMAT"] = rbOutputOxps.Checked ? "Oxps" : rbOutputBoth.Checked ? "Both" : "Xps";
            psi.Environment["GDG_PRINTER_URI"] = "gdg-virtual-printer:oxps";

            Process.Start(psi);
            SetStatus("Applicazione di test avviata.");
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                this,
                ex.Message,
                "Errore test",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }

    private void ValidateForm()
    {
        if (string.IsNullOrWhiteSpace(txtExecutable.Text))
            throw new InvalidOperationException("Selezionare il programma da eseguire.");

        if (!File.Exists(txtExecutable.Text.Trim()))
            throw new FileNotFoundException(
                "Il programma configurato non esiste.",
                txtExecutable.Text.Trim());

        if (string.IsNullOrWhiteSpace(txtJobsDirectory.Text))
            throw new InvalidOperationException("Specificare la cartella OXPS.");

        Directory.CreateDirectory(txtJobsDirectory.Text.Trim());

        if (rbSpecificAccount.Checked)
        {
            if (string.IsNullOrWhiteSpace(txtAccount.Text))
                throw new InvalidOperationException("Specificare l'account di esecuzione.");

            if (string.IsNullOrEmpty(txtPassword.Text))
                throw new InvalidOperationException("Specificare la password dell'account.");
        }
    }

    private void UpdateAccountControls()
    {
        bool enabled = rbSpecificAccount.Checked;
        txtAccount.Enabled = enabled;
        txtPassword.Enabled = enabled;
        btnVerifyCredentials.Enabled = enabled;
    }

    private void BrowseExecutable(object? sender, EventArgs e)
    {
        using var dialog = new OpenFileDialog
        {
            Filter = "Programmi Windows (*.exe)|*.exe|Tutti i file (*.*)|*.*",
            CheckFileExists = true
        };

        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            txtExecutable.Text = dialog.FileName;

            if (string.IsNullOrWhiteSpace(txtWorkingDirectory.Text))
                txtWorkingDirectory.Text = Path.GetDirectoryName(dialog.FileName) ?? string.Empty;
        }
    }

    private void BrowseWorkingDirectory(object? sender, EventArgs e)
    {
        using var dialog = new FolderBrowserDialog();
        if (dialog.ShowDialog(this) == DialogResult.OK)
            txtWorkingDirectory.Text = dialog.SelectedPath;
    }

    private void BrowseJobsDirectory(object? sender, EventArgs e)
    {
        using var dialog = new FolderBrowserDialog
        {
            SelectedPath = txtJobsDirectory.Text
        };

        if (dialog.ShowDialog(this) == DialogResult.OK)
            txtJobsDirectory.Text = dialog.SelectedPath;
    }

    private void SetStatus(string message)
    {
        lblStatus.Text = message;
    }
}
