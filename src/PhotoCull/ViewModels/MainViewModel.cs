using CommunityToolkit.Mvvm.ComponentModel;
using PhotoCull.Data;
using PhotoCull.Models;
using Microsoft.EntityFrameworkCore;

namespace PhotoCull.ViewModels;

public partial class MainViewModel : ObservableObject
{
    [ObservableProperty] private AppPhase _currentPhase = AppPhase.License;
    [ObservableProperty] private bool _isLicenseValid;
    [ObservableProperty] private string _licenseCode = string.Empty;
    [ObservableProperty] private bool _isInspectorVisible = true;
    [ObservableProperty] private Photo? _inspectedPhoto;

    public ImportViewModel ImportVm { get; } = new();
    public CullingViewModel CullingVm { get; } = new();
    public ExportViewModel ExportVm { get; } = new();

    public MainViewModel()
    {
        // Check saved license
        var saved = Properties.Settings.Default.LicenseCode;
        if (!string.IsNullOrEmpty(saved))
        {
            var result = LicenseValidator.Validate(saved, checkActivationWindow: false);
            if (result.Status == LicenseStatus.Valid)
            {
                IsLicenseValid = true;
                LicenseCode = saved;
                CurrentPhase = AppPhase.Import;
            }
        }
    }

    public bool TryActivateLicense(string code)
    {
        var result = LicenseValidator.Validate(code, checkActivationWindow: true);
        if (result.Status == LicenseStatus.Valid)
        {
            LicenseCode = code;
            IsLicenseValid = true;
            Properties.Settings.Default.LicenseCode = code;
            Properties.Settings.Default.Save();
            CurrentPhase = AppPhase.Import;
            return true;
        }
        return false;
    }

    public string? GetLicenseError(string code)
    {
        var result = LicenseValidator.Validate(code, checkActivationWindow: true);
        return result.Status switch
        {
            LicenseStatus.Invalid => "无效的授权码",
            LicenseStatus.Expired => "授权码已过期",
            LicenseStatus.ActivationWindowExpired => "授权码已超过 15 分钟激活窗口",
            _ => null
        };
    }

    public void NavigateToPhase(AppPhase phase)
    {
        CurrentPhase = phase;
    }

    public void OnImportComplete(CullingSession session)
    {
        CullingVm.LoadSession(session);
        ExportVm.LoadSession(session);
        CurrentPhase = AppPhase.QuickCull;
    }

    public void OnQuickCullComplete()
    {
        CullingVm.TransitionToGroupPick();
        CurrentPhase = AppPhase.GroupPick;
        IsInspectorVisible = false;
    }

    public void OnGroupPickComplete()
    {
        CurrentPhase = AppPhase.Export;
    }

    public void BackToQuickCull()
    {
        CullingVm.BackToQuickCull();
        CurrentPhase = AppPhase.QuickCull;
    }

    public async Task<List<CullingSession>> GetRecentSessionsAsync()
    {
        using var db = new PhotoCullDbContext();
        return await db.CullingSessions
            .Where(s => !s.IsCompleted)
            .OrderByDescending(s => s.CreatedAt)
            .Take(5)
            .Include(s => s.Photos)
            .Include(s => s.Groups)
                .ThenInclude(g => g.Photos)
            .ToListAsync();
    }

    public void ResumeSession(CullingSession session)
    {
        CullingVm.LoadSession(session);
        ExportVm.LoadSession(session);
        CurrentPhase = session.CurrentRound == Round.GroupPick ? AppPhase.GroupPick : AppPhase.QuickCull;
    }

    public void ToggleInspector() => IsInspectorVisible = !IsInspectorVisible;
}
