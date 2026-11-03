using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace CodexContinue;

public partial class MainWindow
{
    GitHubUpdates _updates = new();
    readonly CancellationTokenSource _updatesCancellation = new();
    Task? _updateCheckTask, _updateDownloadTask;
    bool _manualUpdateRequested, _updateChecking, _updateDownloading, _updateDetailsOpen, _updateToastDismissed;
    int _updatePageRevision, _updateToastRevision;
    UpdateRelease? _availableUpdate, _toastUpdate, _shownUpdate;
    UpdateRelease? _renderedUpdate;
    Loc.Lang? _renderedUpdateLanguage;
    string? _downloadedUpdatePath;
    string? _downloadedUpdateTag;
    string _updateCheckStatusKey = "", _updateDownloadStatusKey = "";
    string _updateDownloadRoot = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CodexContinue", "Updates");
    static ReleaseVersion InstalledVersion => ReleaseVersion.Parse(typeof(App).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()!.InformationalVersion.Split('+')[0])!;

    void StartUpdateCheck()
    {
        _ = CheckForUpdatesAsync(false);
    }
    Task CheckForUpdatesAsync(bool manual)
    {
        _manualUpdateRequested |= manual;
        if (_updateCheckTask is { IsCompleted: false }) return _updateCheckTask;
        return _updateCheckTask = RunUpdateCheckAsync();
    }
    async Task RunUpdateCheckAsync()
    {
        _updateChecking = true;
        _updateCheckStatusKey = "";
        ApplyUpdateLoc();
        try
        {
            _availableUpdate = await _updates.FindUpdateAsync(InstalledVersion, _updatesCancellation.Token);
            if (_updatesCancellation.IsCancellationRequested) return;
            if (_availableUpdate != null)
            {
                if (_manualUpdateRequested || _updateDetailsOpen) ShowUpdateDetails(_availableUpdate);
                else if (!_updateToastDismissed) ShowUpdateToast(_availableUpdate);
            }
            else if (_manualUpdateRequested) _updateCheckStatusKey = "updates.latest";
        }
        catch (OperationCanceledException) when (_updatesCancellation.IsCancellationRequested) { }
        catch { if (_manualUpdateRequested) _updateCheckStatusKey = "updates.checkFailed"; }
        finally
        {
            _updateChecking = false;
            _manualUpdateRequested = false;
            if (!_updatesCancellation.IsCancellationRequested) ApplyUpdateLoc();
        }
    }
    async void BtnCheckUpdates_Click(object sender, RoutedEventArgs e) => await CheckForUpdatesAsync(true);
    string UpdateVersionLabel(UpdateRelease release) => release.IsPreview ? Loc.T("updates.previewLabel", release.VersionLabel) : release.VersionLabel;
    void ShowUpdateToast(UpdateRelease release)
    {
        if (_updateToastDismissed || _updateDetailsOpen) return;
        _toastUpdate = release;
        ++_updateToastRevision;
        ApplyUpdateLoc();
        UpdateToast.Visibility = Visibility.Visible;
        UpdateToast.IsHitTestVisible = true;
        UpdateToast.RenderTransform = new TranslateTransform();
        UpdateToast.BeginAnimation(OpacityProperty, null);
        UpdateToast.Opacity = 1;
        if (SystemParameters.ClientAreaAnimation)
        {
            UpdateToast.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(240)) { EasingFunction = Motion.SettleOut });
            ((TranslateTransform)UpdateToast.RenderTransform).BeginAnimation(TranslateTransform.YProperty,
                new DoubleAnimation(8, 0, TimeSpan.FromMilliseconds(280)) { EasingFunction = Motion.SettleOut });
        }
    }
    void DismissUpdateToast()
    {
        _updateToastDismissed = true;
        int revision = ++_updateToastRevision;
        UpdateToast.IsHitTestVisible = false;
        if (!SystemParameters.ClientAreaAnimation) { UpdateToast.Visibility = Visibility.Collapsed; return; }
        var fade = new DoubleAnimation(UpdateToast.Opacity, 0, TimeSpan.FromMilliseconds(160)) { EasingFunction = Motion.SoftOut };
        fade.Completed += (_, _) => { if (revision == _updateToastRevision) UpdateToast.Visibility = Visibility.Collapsed; };
        UpdateToast.BeginAnimation(OpacityProperty, fade);
    }
    void BtnUpdateToastClose_Click(object sender, RoutedEventArgs e) => DismissUpdateToast();
    void BtnUpdateToastOpen_Click(object sender, RoutedEventArgs e)
    {
        if (_toastUpdate != null) ShowUpdateDetails(_toastUpdate);
    }
    void ShowUpdateDetails(UpdateRelease release)
    {
        FinishStartupTransition();
        bool entering = !_updateDetailsOpen;
        double from = UpdatePage.Visibility == Visibility.Visible ? UpdatePage.Opacity : 0;
        _shownUpdate = release;
        _updateDetailsOpen = true;
        DismissUpdateToast();
        ConvoPick.IsDropDownOpen = false;
        System.Windows.Input.Keyboard.ClearFocus();
        TitleIcon.Visibility = TitleName.Visibility = Visibility.Visible;
        UpdatePage.Visibility = Visibility.Visible;
        UpdatePage.IsHitTestVisible = true;
        ApplyUpdateLoc();
        if (!entering) return;
        int revision = ++_updatePageRevision;
        void HideMenu()
        {
            if (revision != _updatePageRevision || !_updateDetailsOpen) return;
            TabRow.Visibility = ContentHost.Visibility = Visibility.Collapsed;
            SplashPage.Opacity = ConnectPage.Opacity = 0;
            BtnUpdateBack.Focus();
        }
        UpdatePage.BeginAnimation(OpacityProperty, null);
        UpdatePage.Opacity = 1;
        if (!SystemParameters.ClientAreaAnimation) { HideMenu(); return; }
        var fade = new DoubleAnimation(from, 1, TimeSpan.FromMilliseconds(240)) { EasingFunction = Motion.SettleOut };
        fade.Completed += (_, _) => HideMenu();
        UpdatePage.BeginAnimation(OpacityProperty, fade);
    }
    void BtnUpdateBack_Click(object sender, RoutedEventArgs e)
    {
        _updateDetailsOpen = false;
        int revision = ++_updatePageRevision;
        UpdatePage.IsHitTestVisible = false;
        TabRow.Visibility = ContentHost.Visibility = Visibility.Visible;
        SplashPage.Opacity = ConnectPage.Opacity = 1;
        UpdateMaterialCoverage();
        UpdateNavigation(animate: false);
        void Finish()
        {
            if (revision != _updatePageRevision || _updateDetailsOpen) return;
            UpdatePage.Visibility = Visibility.Collapsed;
            if (SplashPage.Visibility == Visibility.Visible && ConnectPage.Visibility != Visibility.Visible)
                TitleIcon.Visibility = TitleName.Visibility = Visibility.Collapsed;
        }
        if (!SystemParameters.ClientAreaAnimation) { Finish(); return; }
        var fade = new DoubleAnimation(UpdatePage.Opacity, 0, TimeSpan.FromMilliseconds(200)) { EasingFunction = Motion.SoftOut };
        fade.Completed += (_, _) => Finish();
        UpdatePage.BeginAnimation(OpacityProperty, fade);
    }
    void ApplyUpdateLoc()
    {
        BtnCheckUpdates.Content = Loc.T(_updateChecking ? "updates.checking" : "updates.check");
        BtnCheckUpdates.IsEnabled = !_updateChecking;
        UpdateCheckStatus.Text = _updateCheckStatusKey.Length > 0 ? Loc.T(_updateCheckStatusKey) : "";
        UpdateCheckStatus.SetResourceReference(TextBlock.ForegroundProperty, _updateCheckStatusKey == "updates.checkFailed" ? "ErrBrush" : "OkBrush");
        UpdateToastTitle.Text = Loc.T("updates.available");
        BtnUpdateToastClose.ToolTip = Loc.T("updates.dismiss");
        UpdatePageTitle.Text = Loc.T("updates.details");
        BtnUpdateBack.ToolTip = Loc.T("updates.back");
        System.Windows.Automation.AutomationProperties.SetName(BtnUpdateBack, Loc.T("updates.back"));
        System.Windows.Automation.AutomationProperties.SetName(BtnUpdateToastClose, Loc.T("updates.dismiss"));
        if (_toastUpdate != null) UpdateToastSubtitle.Text = Loc.T("updates.notice", UpdateVersionLabel(_toastUpdate), _toastUpdate.PublishedAt.ToLocalTime().ToString("MM-dd"));
        BtnDownloadUpdate.Content = Loc.T(_updateDownloading ? "updates.downloading" : _downloadedUpdatePath != null && _downloadedUpdateTag == _shownUpdate?.TagName ? "updates.openFolder" : "updates.download");
        BtnDownloadUpdate.IsEnabled = !_updateDownloading && _shownUpdate is { IsPreview: false, Asset: not null };
        BtnDownloadUpdate.ToolTip = _shownUpdate?.IsPreview == true ? Loc.T("updates.previewHint") : _shownUpdate?.Asset == null ? Loc.T("updates.noPackage") : null;
        if (_shownUpdate != null)
        {
            UpdateReleaseTitle.Text = _shownUpdate.IsPreview ? UpdateVersionLabel(_shownUpdate) : _shownUpdate.Title;
            UpdateReleaseDate.Text = Loc.T("updates.published", _shownUpdate.PublishedAt.ToLocalTime().ToString("yyyy-MM-dd"));
            if (_renderedUpdate != _shownUpdate || _renderedUpdateLanguage != Loc.Current)
            {
                string body = _shownUpdate.IsPreview ? Loc.T("updates.previewNotes") : string.IsNullOrWhiteSpace(_shownUpdate.Notes) ? Loc.T("updates.noNotes") : _shownUpdate.Notes;
                UpdateNotes.Document = ReleaseNotes.Render(body, OpenUpdateLink);
                _renderedUpdate = _shownUpdate;
                _renderedUpdateLanguage = Loc.Current;
            }
        }
        if (_updateDownloadStatusKey.Length > 0) UpdateDownloadStatus.Text = Loc.T(_updateDownloadStatusKey);
    }
    void OpenUpdateLink(Uri uri)
    {
        try { Process.Start(new ProcessStartInfo(uri.AbsoluteUri) { UseShellExecute = true }); }
        catch { _updateDownloadStatusKey = "updates.openFailed"; UpdateDownloadStatus.Text = Loc.T(_updateDownloadStatusKey); UpdateDownloadFeedback.Visibility = Visibility.Visible; }
    }
    async void BtnDownloadUpdate_Click(object sender, RoutedEventArgs e)
    {
        if (_downloadedUpdatePath != null && _downloadedUpdateTag == _shownUpdate?.TagName)
        {
            try { var info = new ProcessStartInfo("explorer.exe"); info.ArgumentList.Add("/select," + _downloadedUpdatePath); Process.Start(info); }
            catch { OpenUpdateLink(new Uri("https://github.com/Belate-stay/Codex-Continue/releases")); }
            return;
        }
        if (_shownUpdate is not { IsPreview: false, Asset: not null } release || _updateDownloadTask is { IsCompleted: false }) return;
        _updateDownloadTask = DownloadUpdateAsync(release);
        await _updateDownloadTask;
    }
    async Task DownloadUpdateAsync(UpdateRelease release)
    {
        _updateDownloading = true;
        _updateDownloadStatusKey = "updates.preparing";
        UpdateDownloadFeedback.Visibility = Visibility.Visible;
        ApplyUpdateLoc();
        UpdateDownloadFill.RenderTransform = new ScaleTransform(0, 1);
        try
        {
            var progress = new Progress<UpdateDownloadProgress>(value => Dispatcher.BeginInvoke(new Action(() =>
            {
                if (_updatesCancellation.IsCancellationRequested || !_updateDownloading) return;
                _updateDownloadStatusKey = "";
                UpdateDownloadStatus.Text = value.Total is > 0 ? Loc.T("updates.progress", Bytes(value.Received), Bytes(value.Total.Value)) : Loc.T("updates.progressUnknown", Bytes(value.Received));
                double ratio = value.Total is > 0 ? Math.Clamp((double)value.Received / value.Total.Value, 0, 1) : .25;
                ((ScaleTransform)UpdateDownloadFill.RenderTransform).BeginAnimation(ScaleTransform.ScaleXProperty,
                    new DoubleAnimation(ratio, TimeSpan.FromMilliseconds(SystemParameters.ClientAreaAnimation ? 140 : 0)) { EasingFunction = Motion.SettleOut });
            })));
            _downloadedUpdatePath = await _updates.DownloadAsync(release, _updateDownloadRoot, progress, _updatesCancellation.Token);
            _downloadedUpdateTag = release.TagName;
            _updateDownloadStatusKey = "updates.downloaded";
            ((ScaleTransform)UpdateDownloadFill.RenderTransform).BeginAnimation(ScaleTransform.ScaleXProperty, null);
            ((ScaleTransform)UpdateDownloadFill.RenderTransform).ScaleX = 1;
        }
        catch (OperationCanceledException) when (_updatesCancellation.IsCancellationRequested) { }
        catch { _updateDownloadStatusKey = "updates.downloadFailed"; }
        finally
        {
            _updateDownloading = false;
            if (!_updatesCancellation.IsCancellationRequested) ApplyUpdateLoc();
        }
    }
    static string Bytes(long value) => value >= 1024 * 1024 ? $"{value / (1024d * 1024):0.0} MB" : value >= 1024 ? $"{value / 1024d:0.0} KB" : $"{value} B";
}
