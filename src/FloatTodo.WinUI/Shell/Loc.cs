using System;
using Microsoft.Windows.ApplicationModel.Resources;

namespace FloatTodo.WinUI.Shell;

/// <summary>
/// 轻量本地化辅助：包装 ResourceLoader / ResourceManager 提供 C# 代码侧的字符串访问。
/// XAML 侧通过 x:Uid 自动解析 .resw 资源。
/// </summary>
public static class Loc
{
    private static ResourceLoader? _loader;
    private static ResourceManager? _manager;
    private static ResourceContext? _context;
    private static ResourceMap? _map;
    private static string _overrideLanguage = "";

    /// <summary>根据资源键获取本地化字符串。找不到时返回 key 本身。</summary>
    public static string Get(string key)
    {
        try
        {
            if (!string.IsNullOrEmpty(_overrideLanguage))
            {
                _manager ??= new ResourceManager();
                if (_context == null)
                {
                    _context = _manager.CreateResourceContext();
                    _context.QualifierValues["Language"] = _overrideLanguage;
                }
                _map ??= _manager.MainResourceMap.GetSubtree("Resources");
                var candidate = _map?.TryGetValue(key, _context);
                if (candidate != null && !string.IsNullOrEmpty(candidate.ValueAsString))
                {
                    return candidate.ValueAsString;
                }
            }

            _loader ??= new ResourceLoader();
            var value = _loader.GetString(key);
            return string.IsNullOrEmpty(value) ? key : value;
        }
        catch
        {
            return key;
        }
    }

    /// <summary>根据资源键获取带格式占位符的本地化字符串。</summary>
    public static string Format(string key, params object[] args)
    {
        var template = Get(key);
        try { return string.Format(template, args); }
        catch { return template; }
    }

    /// <summary>设置语言覆盖（需重启生效）。</summary>
    public static void SetLanguageOverride(string languageTag)
    {
        _overrideLanguage = languageTag ?? "";
        try
        {
            Microsoft.Windows.Globalization.ApplicationLanguages.PrimaryLanguageOverride = _overrideLanguage;
        }
        catch
        {
            // Windows App SDK PrimaryLanguageOverride may throw in unpackaged mode
        }
        _loader = null;
        _context = null;
    }
}
