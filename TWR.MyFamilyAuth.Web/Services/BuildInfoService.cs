using Microsoft.AspNetCore.Components;
using System.Net.Http.Json;

namespace TWR.MyFamilyAuth.Web.Services;

public class BuildInfoService
{
    private readonly HttpClient        _http;
    private readonly NavigationManager _nav;
    private string? _version;

    public BuildInfoService(HttpClient http, NavigationManager nav)
    {
        _http = http;
        _nav  = nav;
    }

    public async Task<string?> GetVersionAsync()
    {
        if (_version is not null) return _version;
        try
        {
            // Absolute URL so the request goes to the web host (nginx),
            // not the API — the injected HttpClient's BaseAddress points to the API.
            var info = await _http.GetFromJsonAsync<BuildInfo>($"{_nav.BaseUri}buildinfo.json");
            _version = info?.Version;
        }
        catch { }
        return _version;
    }

    private record BuildInfo(string? Version);
}
