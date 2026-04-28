using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CustomResolutionManager.Core.Interfaces;
using CustomResolutionManager.Core.Services;
using CustomResolutionManager.Models;

namespace CustomResolutionManager.ViewModels;

public sealed partial class MainViewModel : ObservableObject
{
    private readonly IGameProfileService _gameProfileService;
    private readonly IResolutionService _resolutionService;

    private GameProfile? _selectedGameProfile;
    private ResolutionOption? _currentResolution;
    private string _statusMessage = string.Empty;

    public MainViewModel()
        : this(new MockGameProfileService(), new MockResolutionService())
    {
    }

    public MainViewModel(IGameProfileService gameProfileService, IResolutionService resolutionService)
    {
        _gameProfileService = gameProfileService;
        _resolutionService = resolutionService;

        AddGameCommand = new AsyncRelayCommand(AddGameAsync);
        EditSelectedGameCommand = new AsyncRelayCommand<GameProfile>(EditSelectedGameAsync);
        DeleteSelectedGameCommand = new AsyncRelayCommand<GameProfile>(DeleteSelectedGameAsync);
        ToggleTrackingCommand = new AsyncRelayCommand<GameProfile>(ToggleTrackingAsync);
        RefreshCommand = new AsyncRelayCommand(LoadAsync);

        LoadAsync().GetAwaiter().GetResult();
    }

    public ObservableCollection<GameProfile> GameProfiles { get; } = new();

    public ObservableCollection<ResolutionOption> AvailableResolutions { get; } = new();

    public GameProfile? SelectedGameProfile
    {
        get => _selectedGameProfile;
        set => SetProperty(ref _selectedGameProfile, value);
    }

    public ResolutionOption? CurrentResolution
    {
        get => _currentResolution;
        private set => SetProperty(ref _currentResolution, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public int TotalProfiles => GameProfiles.Count;

    public int EnabledProfiles => GameProfiles.Count(profile => profile.IsEnabled);

    public ICommand AddGameCommand { get; }

    public AsyncRelayCommand<GameProfile> EditSelectedGameCommand { get; }

    public AsyncRelayCommand<GameProfile> DeleteSelectedGameCommand { get; }

    public AsyncRelayCommand<GameProfile> ToggleTrackingCommand { get; }

    public AsyncRelayCommand RefreshCommand { get; }

    private async Task LoadAsync()
    {
        GameProfiles.Clear();
        foreach (GameProfile profile in await _gameProfileService.GetAllAsync())
        {
            GameProfiles.Add(profile);
        }

        AvailableResolutions.Clear();
        foreach (ResolutionOption resolution in await _resolutionService.GetAvailableResolutionsAsync())
        {
            AvailableResolutions.Add(resolution);
        }

        CurrentResolution = await _resolutionService.GetCurrentResolutionAsync();
        SelectedGameProfile ??= GameProfiles.FirstOrDefault();

        OnPropertyChanged(nameof(TotalProfiles));
        OnPropertyChanged(nameof(EnabledProfiles));

        StatusMessage = $"Loaded {GameProfiles.Count} mock game profiles.";
    }

    private async Task AddGameAsync()
    {
        ResolutionOption nextResolution = AvailableResolutions.FirstOrDefault()
            ?? new ResolutionOption { Width = 1920, Height = 1080, RefreshRate = 60 };

        GameProfile createdProfile = new()
        {
            DisplayName = $"New Game {GameProfiles.Count + 1}",
            ExePath = @"C:\Games\NewGame\NewGame.exe",
            TargetWidth = nextResolution.Width,
            TargetHeight = nextResolution.Height,
            IsEnabled = true,
            IconPath = null
        };

        GameProfile addedProfile = await _gameProfileService.CreateAsync(createdProfile);
        GameProfiles.Add(addedProfile);
        SelectedGameProfile = addedProfile;
        OnPropertyChanged(nameof(TotalProfiles));
        OnPropertyChanged(nameof(EnabledProfiles));
        StatusMessage = $"Added {addedProfile.DisplayName}.";
    }

    private async Task EditSelectedGameAsync(GameProfile? profile)
    {
        if (profile is null)
        {
            return;
        }

        ResolutionOption nextResolution = AvailableResolutions
            .SkipWhile(resolution => resolution.Width != profile.TargetWidth || resolution.Height != profile.TargetHeight)
            .Skip(1)
            .FirstOrDefault()
            ?? AvailableResolutions.FirstOrDefault()
            ?? new ResolutionOption { Width = 1920, Height = 1080, RefreshRate = 60 };

        GameProfile updatedProfile = profile with
        {
            DisplayName = $"{profile.DisplayName} (Edited)",
            TargetWidth = nextResolution.Width,
            TargetHeight = nextResolution.Height
        };

        await _gameProfileService.UpdateAsync(updatedProfile);
        ReplaceProfile(updatedProfile);
        if (SelectedGameProfile?.Id == updatedProfile.Id)
        {
            SelectedGameProfile = updatedProfile;
        }

        StatusMessage = $"Updated {updatedProfile.DisplayName}.";
    }

    private async Task DeleteSelectedGameAsync(GameProfile? profile)
    {
        if (profile is null)
        {
            return;
        }

        await _gameProfileService.DeleteAsync(profile.Id);
        GameProfiles.Remove(profile);
        SelectedGameProfile = GameProfiles.FirstOrDefault();
        OnPropertyChanged(nameof(TotalProfiles));
        OnPropertyChanged(nameof(EnabledProfiles));
        StatusMessage = $"Deleted {profile.DisplayName}.";
    }

    private async Task ToggleTrackingAsync(GameProfile? profile)
    {
        if (profile is null)
        {
            return;
        }

        GameProfile updatedProfile = profile with { IsEnabled = !profile.IsEnabled };
        await _gameProfileService.ToggleEnabledAsync(profile.Id, updatedProfile.IsEnabled);
        ReplaceProfile(updatedProfile);

        if (SelectedGameProfile?.Id == updatedProfile.Id)
        {
            SelectedGameProfile = updatedProfile;
        }

        OnPropertyChanged(nameof(TotalProfiles));
        OnPropertyChanged(nameof(EnabledProfiles));
        StatusMessage = $"Tracking {(updatedProfile.IsEnabled ? "enabled" : "disabled")} for {updatedProfile.DisplayName}.";
    }

    private void ReplaceProfile(GameProfile updatedProfile)
    {
        int index = GameProfiles.ToList().FindIndex(profile => profile.Id == updatedProfile.Id);
        if (index >= 0)
        {
            GameProfiles[index] = updatedProfile;
        }
    }
}