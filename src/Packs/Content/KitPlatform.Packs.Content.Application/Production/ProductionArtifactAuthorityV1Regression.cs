namespace KitPlatform.Packs.Content.Production;

/// <summary>
/// PRODUCTION-STORAGE-ARTIFACT-AUTHORITY-01.
/// Official bytes are reached through a tenant-owned StorageKey. A file on disk is not an artifact.
/// </summary>
public static class ProductionArtifactAuthorityV1Regression
{
    public const string SuiteId = "FAMIXA_PRODUCTION_ARTIFACT_AUTHORITY_01";

    public static IReadOnlyList<string> Run()
    {
        var fail = new List<string>();
        void Ok(bool cond, string name)
        {
            if (!cond) fail.Add(name);
        }

        try
        {
            Prove(Ok);
        }
        catch (Exception ex)
        {
            fail.Add("UNCAUGHT:" + ex.GetType().Name + ":" + ex.Message);
        }

        return fail;
    }

    static void Prove(Action<bool, string> Ok)
    {
        var tenantA = Guid.Parse("aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaa1");
        var tenantB = Guid.Parse("bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbb2");
        var caller = new SwitchCaller { Id = tenantA };
        var files = new InMemoryProductionArtifactFileStore(caller);
        var bytesA = Sentinel(1);
        var bytesB = Sentinel(2);
        var keyA = files.Write("os", "authoritya", bytesA);
        caller.Id = tenantB;
        Ok(files.Read(keyA) is null, "tenant B cannot read tenant A storage key");
        var keyB = files.Write("os", "authoritya", bytesB);
        Ok(!string.Equals(keyA, keyB, StringComparison.OrdinalIgnoreCase)
            && ProductionTenantBoundary.OwnsStorageKey(tenantB, keyB)
            && !ProductionTenantBoundary.OwnsStorageKey(tenantA, keyB),
            "tenant B write stays in tenant B namespace");
        caller.Id = tenantA;
        Ok(files.Read(keyA)!.SequenceEqual(bytesA) && files.Read(keyB) is null,
            "tenant A cannot read tenant B storage key");
        Ok(files.Read("../" + keyA) is null && files.Read(keyA + "/../../other") is null,
            "path traversal is not an official read");

        var sha = KitVideoIntegrityRules.Sha256Hex(bytesA);
        Ok(OfficialPreviewIntegrityV1.ReadVerifiedPhysicalBytes(files, keyA, sha)!.SequenceEqual(bytesA),
            "preview accepts matching physical bytes");
        Ok(OfficialPreviewIntegrityV1.ReadVerifiedPhysicalBytes(files, keyA, new string('a', 64)) is null,
            "preview rejects a SHA mismatch");
        Ok(OfficialPreviewIntegrityV1.ReadVerifiedPhysicalBytes(files, keyA + "-missing", sha) is null,
            "missing physical file fails closed");
        Ok(Throws(ProductionArtifactPhysicalWrite.Collision, () => files.Write("os", "authoritya", bytesB)),
            "same storage key rejects different bytes");

        var root = RepoRoot();
        var directory = File.ReadAllText(Path.Combine(root,
            "src", "Packs", "Content", "KitPlatform.Packs.Content.Infrastructure", "Production", "OfficialVeoPollRecoverMaterializationLiveV1.cs"));
        var writer = directory.IndexOf("class DirectoryProductionArtifactFileStore", StringComparison.Ordinal);
        var write = directory.IndexOf("CloseUnprefixedProductionWrite", writer, StringComparison.Ordinal);
        Ok(writer > 0 && write > writer, "directory production writer is closed");
        Ok(Throws(ProductionArtifactPhysicalWrite.UnprefixedWriteClosed, () =>
            ProductionArtifactPhysicalWrite.CloseUnprefixedProductionWrite()),
            "unprefixed official write fails closed");
        var store = File.ReadAllText(Path.Combine(root,
            "src", "Packs", "Content", "KitPlatform.Packs.Content.Infrastructure", "Production", "ProductionArtifactFileStore.cs"));
        var create = File.ReadAllText(Path.Combine(root,
            "src", "Packs", "Content", "KitPlatform.Packs.Content.Application", "Production", "ProductionCreateVideoService.cs"));
        var repository = File.ReadAllText(Path.Combine(root,
            "src", "Packs", "Content", "KitPlatform.Packs.Content.Infrastructure", "Production", "ProductionWorkRepository.cs"));
        Ok(store.Contains("ProductionTenantBoundary.Prefix", StringComparison.Ordinal)
            && store.Contains("OwnsStorageKey", StringComparison.Ordinal)
            && !store.Contains("GetOrgSettingsAsync", StringComparison.Ordinal),
            "official file store prefixes and checks the caller tenant");
        var writeAt = create.IndexOf("_files.Write", StringComparison.Ordinal);
        var verifyAt = create.IndexOf("VerifyArtifactAsync", StringComparison.Ordinal);
        Ok(writeAt >= 0 && verifyAt > writeAt, "official materialization writes bytes before the artifact row");
        Ok(repository.Contains("OwnsStorageKey", StringComparison.Ordinal)
            && repository.Split("INSERT INTO pack_content.production_artifacts").Length == 2,
            "artifact rows are inserted in one place and reject a foreign storage key");
    }

    static byte[] Sentinel(byte mark)
    {
        var bytes = new byte[32];
        bytes[0] = mark;
        return bytes;
    }

    static bool Throws(string code, Action action)
    {
        try
        {
            action();
            return false;
        }
        catch (InvalidOperationException ex)
        {
            return ex.Message == code;
        }
    }

    static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "src", "KitPlatform.Api", "KitPlatform.Api.csproj")))
                return dir.FullName;
            dir = dir.Parent;
        }
        return @"E:\KitPlatform";
    }

    sealed class SwitchCaller : IProductionCallerTenant
    {
        public Guid Id { get; set; }
        public Guid RequireTenant() => ProductionTenantBoundary.Require(Id);
    }
}
