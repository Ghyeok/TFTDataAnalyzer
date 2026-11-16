using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using TFTAnalyzer.Domain;
using TFTAnalyzer.Editor.Diagnostics;
using TFTAnalyzer.Editor.Persistence;
using TFTAnalyzer.Editor.RiotApi;
using TFTAnalyzer.Serialization;
using TFTAnalyzer.Tests.UI;
using TFTAnalyzer.Validation;
using UnityEngine;
using static TFTAnalyzer.Tests.Diagnostics.MatchShapeTestSupport;

namespace TFTAnalyzer.Tests.Diagnostics
{
    /// <summary>TODO 6: the entry point never changes analyzer stores, catalogs or the index; the diagnostic never references Persistence.</summary>
    public sealed class MatchShapeStoreInvarianceTests
    {
        private string _root;
        private StaticCatalogPaths _paths;
        private CatalogIndex _index;

        [SetUp]
        public void SetUp()
        {
            _root = CreateTempRoot();
            _paths = new StaticCatalogPaths(_root);
            _index = ScriptableObject.CreateInstance<CatalogIndex>();
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(_index);
            DeleteTempRoot(_root);
        }

        private string StoreRoot => new AnalyzerStorePaths(_root).RootDirectory;

        private void PublishCatalog(string version = "16.18.1", string completed = "2026-11-01T00:00:00.0000000Z")
        {
            var identity = DataManagerTestSupport.Identity(version);
            var catalog = DataManagerTestSupport.Catalog(identity);
            var writer = new CatalogAssetWriter(_paths);
            var staging = _paths.CreateStagingDirectory();
            var staged = writer.WriteStagedCatalog(catalog, new CatalogValidator().Validate(catalog), staging);
            writer.Publish(staged, _paths.CanonicalFinalDirectory(identity));
            writer.CleanupStaging(staging);
            _index.Upsert(identity, staged.ContentHash, _paths.CatalogAssetPath(identity), false, completed);
        }

        /// <summary>Creates existing store files in every protected location so changes, deletions and lock files are visible.</summary>
        private void SeedStore()
        {
            void Write(string relative, string text)
            {
                var path = Path.Combine(StoreRoot, relative);
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllText(path, text);
            }
            Write(Path.Combine("observations", new string('a', 64) + ".ndjson"), "{\"existing\":1}\n{\"tail\":");
            Write("processed-match-hashes.json", "{\"version\":1,\"hashes\":[]}");
            Write(Path.Combine("incomplete-matches", new string('b', 32) + ".json"), "{\"existing\":true}");
            Write(Path.Combine("diagnostics", "quarantine", new string('c', 64) + ".json"), "{\"quarantine\":true}");
            Write(Path.Combine("aggregates", "1", new string('d', 64) + ".json"), "{}");
            File.WriteAllText(Path.Combine(_paths.AbsoluteAssetPath(_paths.GeneratedRelativeDirectory), "CatalogIndex.asset"), "index-bytes");
        }

        /// <summary>Path, size and SHA-256 of every file outside the diagnostic report directory.</summary>
        private string[] Tree()
        {
            var excluded = Path.Combine(_root, "Library", "TftAnalyzerDiagnostics") + Path.DirectorySeparatorChar;
            return Directory.GetFileSystemEntries(_root, "*", SearchOption.AllDirectories)
                .Where(path => !path.StartsWith(excluded, StringComparison.OrdinalIgnoreCase) &&
                               !string.Equals(path + Path.DirectorySeparatorChar, excluded, StringComparison.OrdinalIgnoreCase))
                .OrderBy(path => path, StringComparer.Ordinal)
                .Select(path => Directory.Exists(path) ? path.Substring(_root.Length) + "/"
                    : path.Substring(_root.Length) + ":" + new FileInfo(path).Length + ":" + ContentHasher.Sha256Hex(File.ReadAllBytes(path)))
                .ToArray();
        }

        private string[] IndexEntries() => _index.Entries.Select(entry => string.Join("|", entry.SetId, entry.SetNumber,
            entry.PatchId, entry.SourceGameVersion, entry.SourceProfileId, entry.ContentHash, entry.CatalogAssetPath, entry.ImportCompletedUtc)).ToArray();

        private (MatchShapeDiagnosticEntryPoint Entry, DiagnosticHarness Harness) EntryPoint(int ids = 3, string key = FakeKey)
        {
            var harness = new DiagnosticHarness(ids)
            {
                Key = () => key,
                SinkOverride = new MatchShapeReportFileSink(_root, new[] { StoreRoot })
            };
            return (new MatchShapeDiagnosticEntryPoint(new PublishedCatalogMatchShapeSource(_paths, () => _index), harness.Build), harness);
        }

        [Test]
        public async Task SuccessFailureCancellationAndPreconditionPathsLeaveEveryStoreFileUnchanged()
        {
            PublishCatalog();
            SeedStore();
            var before = Tree();
            var indexBefore = IndexEntries();
            Assert.That(before.Any(line => line.Contains("observations")), Is.True);
            Assert.That(before.Any(line => line.Contains("catalog.json")), Is.True);

            var (success, successHarness) = EntryPoint(12);
            var completed = await success.RunAsync(CancellationToken.None);
            Assert.That(completed.Status, Is.EqualTo("completed"));
            Assert.That(completed.ReportPublished, Is.True);
            Assert.That(successHarness.Transport.Sent, Has.Count.EqualTo(13));
            Assert.That(Tree(), Is.EqualTo(before), "success");

            var (failure, failureHarness) = EntryPoint();
            failureHarness.Transport.Respond = (uri, i) => Status(500);
            Assert.That((await failure.RunAsync(CancellationToken.None)).ReasonCode, Is.EqualTo("list_server_error"));
            Assert.That(Tree(), Is.EqualTo(before), "failure");

            var (cancel, cancelHarness) = EntryPoint(5);
            using (var cancellation = new CancellationTokenSource())
            {
                cancelHarness.Transport.BeforeRespond = index => { if (index == 2) cancellation.Cancel(); };
                Assert.That((await cancel.RunAsync(cancellation.Token)).Status, Is.EqualTo("cancelled"));
            }
            Assert.That(Tree(), Is.EqualTo(before), "cancel");

            var (precondition, preconditionHarness) = EntryPoint(key: null);
            Assert.That((await precondition.RunAsync(CancellationToken.None)).Status, Is.EqualTo("precondition_failed"));
            Assert.That(preconditionHarness.Transport.Sent, Is.Empty);
            Assert.That(Tree(), Is.EqualTo(before), "precondition");
            Assert.That(IndexEntries(), Is.EqualTo(indexBefore));
        }

        [Test]
        public async Task EntryPointBuildsInputsFromTheVerifiedCatalogAndStopsBeforeNetworkWithoutOne()
        {
            var (missing, missingHarness) = EntryPoint();
            var none = await missing.RunAsync(CancellationToken.None);
            Assert.That(none.Status, Is.EqualTo("precondition_failed"));
            Assert.That(none.ReasonCode, Is.EqualTo("catalog_unavailable"));
            Assert.That(missingHarness.Transport.Sent, Is.Empty);
            Assert.That(missingHarness.Confirmation.Calls, Is.Zero);

            PublishCatalog("16.18.1", "2026-11-01T00:00:00.0000000Z");
            PublishCatalog("16.19.2", "2026-11-02T00:00:00.0000000Z");
            var selection = new PublishedCatalogMatchShapeSource(_paths, () => _index).Load(out var code);
            Assert.That(code, Is.Null);
            Assert.That(selection.SetNumber, Is.EqualTo(18));
            Assert.That(selection.Release, Is.EqualTo("16.19"), "The most recently imported verified catalog is selected.");
            Assert.That(selection.UnitIds, Is.EqualTo(new[] { "TEST18_Unit" }));

            var (entry, harness) = EntryPoint(1);
            var result = await entry.RunAsync(CancellationToken.None);
            Assert.That(result.Status, Is.EqualTo("completed"));
            Assert.That(harness.Confirmation.Summary, Does.Contain("16.19"));
            Assert.That(harness.Confirmation.Summary, Does.Contain("1100"));
            Assert.That(harness.Transport.Sent[0].Uri.Host, Is.EqualTo("asia.api.riotgames.com"));
        }

        [Test]
        public void CatalogSourceRejectsAmbiguousOrUnusableCatalogsBeforeNetwork()
        {
            PublishCatalog("16.18.1", null);
            PublishCatalog("16.19.2", null);
            Assert.That(new PublishedCatalogMatchShapeSource(_paths, () => _index).Load(out var ambiguous), Is.Null);
            Assert.That(ambiguous, Is.EqualTo("catalog_ambiguous"));
            Assert.That(new PublishedCatalogMatchShapeSource(_paths, () => null).Load(out var unavailable), Is.Null);
            Assert.That(unavailable, Is.EqualTo("catalog_unavailable"));
            Assert.That(new PublishedCatalogMatchShapeSource(_paths, () => throw new IOException(FakeKey)).Load(out var thrown), Is.Null);
            Assert.That(thrown, Is.EqualTo("catalog_unavailable"));
        }

        private static readonly Type[] PersistenceBoundary = { typeof(PublishedCatalogMatchShapeSource), typeof(MatchShapeDiagnosticMenu) };

        private static bool IsBoundary(Type type)
        {
            for (var current = type; current != null; current = current.DeclaringType)
                if (PersistenceBoundary.Contains(current)) return true;
            return false;
        }

        private static IEnumerable<Type> DiagnosticTypes() => typeof(MatchShapeDiagnostic).Assembly.GetTypes()
            .Where(type => type.Namespace == "TFTAnalyzer.Editor.Diagnostics" && !IsBoundary(type));

        private static bool Forbidden(Type type)
        {
            if (type == null) return false;
            if (type.HasElementType) return Forbidden(type.GetElementType());
            if (type.IsGenericType && type.GetGenericArguments().Any(Forbidden)) return true;
            return (type.Namespace ?? "").StartsWith("TFTAnalyzer.Editor.Persistence", StringComparison.Ordinal) ||
                   type == typeof(RiotApiClient) || type == typeof(MatchCollectionService) ||
                   type.Name == "ObservationStore" || type.Name == "IncompleteMatchStore";
        }

        private static bool Forbidden(MemberInfo member)
        {
            if (member is Type type) return Forbidden(type);
            if (Forbidden(member.DeclaringType)) return true;
            if (member.DeclaringType == typeof(MatchAnonymizer) && member is MethodBase anonymizerMethod &&
                (anonymizerMethod.Name == "Read" || anonymizerMethod.Name == "ReadWithIncomplete" || anonymizerMethod.IsConstructor)) return true;
            switch (member)
            {
                case FieldInfo field: return Forbidden(field.FieldType);
                case MethodInfo method: return Forbidden(method.ReturnType) || method.GetParameters().Any(p => Forbidden(p.ParameterType));
                case ConstructorInfo constructor: return constructor.GetParameters().Any(p => Forbidden(p.ParameterType));
            }
            return false;
        }

        private static readonly Dictionary<short, OpCode> OpCodesByValue = typeof(OpCodes)
            .GetFields(BindingFlags.Public | BindingFlags.Static).Select(field => (OpCode)field.GetValue(null))
            .ToDictionary(code => code.Value);

        /// <summary>Resolves every type, field and method token in a method body.</summary>
        private static List<MemberInfo> References(MethodBase method)
        {
            var result = new List<MemberInfo>();
            var il = method.GetMethodBody()?.GetILAsByteArray();
            if (il == null) return result;
            var typeArguments = method.DeclaringType != null && method.DeclaringType.IsGenericType ? method.DeclaringType.GetGenericArguments() : null;
            var methodArguments = method.IsGenericMethod ? method.GetGenericArguments() : null;
            for (var i = 0; i < il.Length;)
            {
                var code = il[i] == 0xFE ? OpCodesByValue[unchecked((short)(0xFE00 | il[i + 1]))] : OpCodesByValue[il[i]];
                i += code.Size;
                switch (code.OperandType)
                {
                    case OperandType.InlineNone: break;
                    case OperandType.ShortInlineBrTarget: case OperandType.ShortInlineI: case OperandType.ShortInlineVar: i += 1; break;
                    case OperandType.InlineVar: i += 2; break;
                    case OperandType.InlineI8: case OperandType.InlineR: i += 8; break;
                    case OperandType.InlineSwitch: i += 4 + 4 * BitConverter.ToInt32(il, i); break;
                    case OperandType.InlineField: case OperandType.InlineMethod: case OperandType.InlineType: case OperandType.InlineTok:
                        var token = BitConverter.ToInt32(il, i);
                        i += 4;
                        try { result.Add(method.Module.ResolveMember(token, typeArguments, methodArguments)); }
                        catch (ArgumentException) { }
                        break;
                    default: i += 4; break;
                }
            }
            return result;
        }

        private static List<string> Violations(Type type)
        {
            const BindingFlags all = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;
            var violations = new List<string>();
            if (Forbidden(type.BaseType) || type.GetInterfaces().Any(Forbidden)) violations.Add(type.FullName + " base");
            foreach (var field in type.GetFields(all).Where(Forbidden)) violations.Add(type.FullName + "." + field.Name);
            foreach (var property in type.GetProperties(all).Where(p => Forbidden(p.PropertyType))) violations.Add(type.FullName + "." + property.Name);
            foreach (var method in type.GetConstructors(all).Cast<MethodBase>().Concat(type.GetMethods(all)))
            {
                if (Forbidden(method)) violations.Add(type.FullName + "." + method.Name + " signature");
                foreach (var reference in References(method).Where(Forbidden))
                    violations.Add(type.FullName + "." + method.Name + " -> " + reference.DeclaringType?.FullName + "." + reference.Name);
            }
            return violations;
        }

        [Test]
        public void DiagnosticTypesReferenceNoPersistenceStoreOrCollectionPath()
        {
            var types = DiagnosticTypes().ToArray();
            Assert.That(types, Does.Contain(typeof(MatchShapeDiagnostic)));
            Assert.That(types.Any(type => type.Name.Contains("<RunAsync>")), Is.True, "Async state machines are scanned too.");
            var violations = types.SelectMany(Violations).ToArray();
            Assert.That(violations, Is.Empty);
        }

        [Test]
        public void ReferenceScannerDetectsTheReadOnlyCatalogBoundary()
        {
            Assert.That(Violations(typeof(PublishedCatalogMatchShapeSource)), Is.Not.Empty,
                "Negative control: the catalog adapter deliberately uses the read-only Persistence loader.");
        }
    }
}
