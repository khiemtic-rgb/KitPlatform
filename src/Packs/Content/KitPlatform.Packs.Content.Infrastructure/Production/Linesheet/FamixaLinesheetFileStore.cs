using System.Reflection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using KitPlatform.Packs.Content.Production;

namespace KitPlatform.Packs.Content.Infrastructure;

/// <summary>
/// Kho file M1: <c>{Root}/{tenantN}/{productionN}/...</c>. Khóa lưu trữ luôn bắt đầu bằng tiền tố tenant
/// (<see cref="ProductionTenantBoundary.Prefix"/>); mở khóa của tenant khác bị từ chối, và đường dẫn không thể thoát khỏi thư mục gốc.
/// Font Be Vietnam Pro (từ 2026-11-05, trước là DejaVu) nhúng trong assembly được giải nén một lần vào <c>{Root}/_fonts</c> (thẻ chữ và phụ đề đốt vào hình dùng chung).
/// </summary>
internal sealed class FamixaLinesheetFileStore : IFamixaLinesheetFileStore
{
    const string FontResourcePrefix = "Linesheet.Fonts.";
    static readonly object FontLock = new();

    readonly string _root;

    public FamixaLinesheetFileStore(IOptions<FamixaLinesheetOptions> options, IHostEnvironment env)
        : this(Path.IsPathRooted(options.Value.Root)
            ? options.Value.Root
            : Path.Combine(env.ContentRootPath, options.Value.Root))
    {
    }

    internal FamixaLinesheetFileStore(string root)
    {
        _root = Path.GetFullPath(root);
        Directory.CreateDirectory(_root);
        FontsDirectory = Path.Combine(_root, "_fonts");
        FontRegularPath = Path.Combine(FontsDirectory, "BeVietnamPro-SemiBold.ttf");
        FontBoldPath = Path.Combine(FontsDirectory, "BeVietnamPro-ExtraBold.ttf");
        EnsureFonts();
    }

    public string FontsDirectory { get; }
    public string FontFamily => "Be Vietnam Pro";
    public string FontRegularPath { get; }
    public string FontBoldPath { get; }

    public string Key(Guid tenant, Guid productionId, string relativePath)
    {
        var segments = (relativePath ?? "").Replace('\\', '/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length == 0 || segments.Any(s => !IsSafeSegment(s)))
            throw new InvalidOperationException("LINESHEET_BAD_PATH: " + relativePath);
        return $"{ProductionTenantBoundary.Prefix(tenant)}/{productionId:N}/{string.Join('/', segments)}";
    }

    public string PathFor(Guid tenant, string storageKey)
    {
        if (!ProductionTenantBoundary.OwnsStorageKey(tenant, storageKey))
            throw new InvalidOperationException(ProductionTenantBoundary.Forbidden);
        var segments = storageKey.Trim().Replace('\\', '/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Any(s => !IsSafeSegment(s)))
            throw new InvalidOperationException("LINESHEET_BAD_PATH");
        var path = Path.GetFullPath(Path.Combine([_root, .. segments]));
        if (!path.StartsWith(_root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("LINESHEET_BAD_PATH");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        return path;
    }

    public string WorkDirectory(Guid tenant, Guid productionId, string name)
    {
        var dir = PathFor(tenant, Key(tenant, productionId, "work/" + name));
        Directory.CreateDirectory(dir);
        return dir;
    }

    /// <summary>Chữ, số, '-', '_', '.'; không phải "." hay "..".</summary>
    internal static bool IsSafeSegment(string s) =>
        s.Length is > 0 and <= 120 && s is not "." and not ".."
        && s.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_' or '.');

    void EnsureFonts()
    {
        lock (FontLock)
        {
            Directory.CreateDirectory(FontsDirectory);
            var asm = typeof(FamixaLinesheetFileStore).Assembly;
            foreach (var name in asm.GetManifestResourceNames().Where(n => n.StartsWith(FontResourcePrefix, StringComparison.Ordinal)))
            {
                var target = Path.Combine(FontsDirectory, name[FontResourcePrefix.Length..]);
                using var src = asm.GetManifestResourceStream(name)!;
                if (File.Exists(target) && new FileInfo(target).Length == src.Length) continue;
                var tmp = target + ".partial";
                using (var dst = File.Create(tmp)) src.CopyTo(dst);
                File.Move(tmp, target, overwrite: true);
            }
        }
    }
}
