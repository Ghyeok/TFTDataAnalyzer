# Lead verification before independent review verdict

Date: 2026-09-24. No source mutation during these checks.

## Independent supplemental regression

Command: `dotnet run --project 'Library/SourceProfileOfflineTests/SourceProfileOfflineTests.csproj' --no-restore`

Exit: 0. Supplemental evidence only; not Unity Editor/EditMode execution.

```text
PASS Compare_ReportsAddedRemovedAndChangedFields
PASS Compare_IgnoresSourceRetrievalTimestampOnlyChanges
PASS DifferentSourceVersionDoesNotShareIdentity
PASS DifferentProfileDoesNotShareIdentity
PASS StableProfileIdRoundTrips
PASS StableProfileIdRoundTrips
PASS UnknownProfileIdIsRejected
PASS SchemaTwoCatalogSerializesProfileAndRejectsUnknownProfile
PASS LocalizedText_UsesEnglishWhenKoreanIsBlank
PASS LocalizedText_PrefersKoreanWhenBothLanguagesExist
PASS PatchKey_RejectsInvalidStableParts
PASS PatchKey_RejectsInvalidStableParts
PASS PatchKey_RejectsInvalidStableParts
PASS PatchKey_RejectsNonPositiveSetNumber
PASS UnitRecord_NormalizesNullCollectionsAndRejectsBlankId
PASS StaticCatalog_CopiesInputCollections
PASS Merge_PreservesOfficialValuesAndFillsMissingFields
PASS Parse_SelectsSetNumberAndStableIdPrefix
PASS Endpoints_DerivePinnedMajorMinorAndRefuseDisabledRequests
PASS Parse_JoinsLocalesAndFiltersRequestedSetPrefix
PASS Parse_UsesEnglishFallbackWhenKoreanEntryIsBlank
PASS Endpoints_PinVersionAndLocale
PASS FetchAsync_RequestsAllFourDocumentsForBothLocales
PASS HttpTransportException_DoesNotExposeResponseBody
PASS RunAsync_DoesNotPublishWhenValidationFails
PASS RunAsync_IsIdempotentForAnIdenticalPatch
PASS RunAsync_ProfilesAndVersionsPublishToFourIndependentDirectories
PASS RunAsync_IdenticalOfficialLegacyRerunUsesVerifiedExistingDirectory
PASS RunAsync_IndexFailureReportsRecoverableUnindexedDirectory
PASS RunAsync_ChangedStandaloneManifestIdentityCannotReturnIdempotent
PASS RunAsync_RejectsSupplementedProfileWhenNoSupplementalSourceWasLoaded
PASS RunAsync_InvalidIconKeepsExistingOutputAndIndex
PASS RunAsync_CancellationKeepsExistingOutputAndIndex
PASS RunAsync_CancellationAfterIconResponseDoesNotPublish
PASS RunAsync_RefreshFailureReportsPublishedDirectoryForRecovery
PASS RunAsync_IsIdempotentWhenOnlyRetrievalTimeChanges
PASS RunAsync_RejectsDifferentContentForExistingPatch
PASS RunAsync_WritesContentAddressedPngAndUpdatesCatalogReference
PASS RunAsync_CleansStagingWhenCancelled
PASS Paths_RejectUnsafePatchSegments
PASS Paths_RejectUnsafePatchSegments
PASS DefaultSummaryNamesFullIdentityAndResolvedPath
PASS FailureStatusIncludesIdentityAndTargetWithoutExceptionBody
PASS CancelledConfirmationDoesNotRunImportAndResetsConsent
PASS SuccessResetsConsentAndPassesSelectedProfile
PASS FailureResetsConsent
PASS CancellationResetsConsent
PASS OfficialOnlyNetworkSourceNeverRequestsCommunityDragonHost
PASS RejectedConfirmationNeverStartsNetworkSource
PASS PublishRejectsTamperedExistingCatalogEvenWhenManifestHashMatchesStagedHash
PASS FourIdentitiesForOnePatchCoexistAndExactUpsertReplacesOnlyOne
PASS CanonicalPathsSeparateVersionAndProfile
PASS VerifiedLegacyUpsertReplacesOldUnprofiledEntryWithoutRemovingSibling
PASS UnprofiledEntryCannotBeSilentlyReadAsOfficial
PASS SaveFailureRestoresExistingIndexBytesAndEntries
PASS IndexWriterRejectsMissingCatalogTarget
PASS UnsafeIdentitySegmentCannotBecomePath
PASS UnsafeIdentitySegmentCannotBecomePath
PASS UnsafeIdentitySegmentCannotBecomePath
PASS UnsafeIdentitySegmentCannotBecomePath
PASS OfficialSchemaOneWithPascalCaseSourcesResolvesExistingPath
PASS SupplementalSchemaOneIsAmbiguous
PASS EmbeddedSupplementalManifestCannotBeClaimedAsOfficial
PASS MissingEmbeddedManifestIsAmbiguous
PASS CommunityVersionInSchemaOneIsAmbiguous
PASS MissingManifestIsAmbiguous
PASS DifferentSourceVersionDoesNotClaimLegacy
PASS MalformedManifestIsAmbiguous
PASS ModifiedLegacyCatalogBytesCannotBeClassifiedCompatible
PASS CommittedSet18LegacyCatalogResolvesWithoutChangingItsBytes
PASS Serialize_SortsRecordsReferencesAndEffectKeys
PASS Deserialize_RoundTripsCanonicalCatalog
PASS Sha256Hex_ReturnsKnownVector
PASS Validate_BlocksDuplicateIdsAndMissingTraits
PASS Validate_ReportsInvalidNamesCostsItemReferencesAndIcons
Offline focused tests: 75 passed, 0 failed
```
## Independent Unity-reference compile

Command: `dotnet build 'Library/SourceProfileNativeCompile/SourceProfileNativeCompile.csproj' --no-restore --nologo --verbosity quiet`

Exit: 0. Supplemental evidence only; not Unity Editor/EditMode execution.

```text

빌드했습니다.
    경고 0개
    오류 0개

경과 시간: 00:00:00.76

워크로드 업데이트를 사용할 수 있습니다. 자세한 내용을 보려면 `dotnet workload list`을 실행하세요.
```

Protected catalog/manifest/both metas and unrelated volume SHA-256 match the values in DevelopmentLead.md. Scoped `git diff --check` output had only LF-to-CRLF notices; generated-data diff was empty. The .NET workload update notice is informational, not a compiler warning.


## Final fix-round-1 independent verification

These checks ran on the fixed source while the same reviewer performed read-only re-review. The three exclusions and native Unity limitations remain unchanged.
### offline

Command: `dotnet run --project 'Library/SourceProfileOfflineTests/SourceProfileOfflineTests.csproj' --no-restore`

Exit: 0.

```text
PASS Compare_ReportsAddedRemovedAndChangedFields
PASS Compare_IgnoresSourceRetrievalTimestampOnlyChanges
PASS DifferentSourceVersionDoesNotShareIdentity
PASS DifferentProfileDoesNotShareIdentity
PASS StableProfileIdRoundTrips
PASS StableProfileIdRoundTrips
PASS UnknownProfileIdIsRejected
PASS SchemaTwoCatalogSerializesProfileAndRejectsUnknownProfile
PASS LocalizedText_UsesEnglishWhenKoreanIsBlank
PASS LocalizedText_PrefersKoreanWhenBothLanguagesExist
PASS PatchKey_RejectsInvalidStableParts
PASS PatchKey_RejectsInvalidStableParts
PASS PatchKey_RejectsInvalidStableParts
PASS PatchKey_RejectsNonPositiveSetNumber
PASS UnitRecord_NormalizesNullCollectionsAndRejectsBlankId
PASS StaticCatalog_CopiesInputCollections
PASS Merge_PreservesOfficialValuesAndFillsMissingFields
PASS Parse_SelectsSetNumberAndStableIdPrefix
PASS Endpoints_DerivePinnedMajorMinorAndRefuseDisabledRequests
PASS Parse_JoinsLocalesAndFiltersRequestedSetPrefix
PASS Parse_UsesEnglishFallbackWhenKoreanEntryIsBlank
PASS Endpoints_PinVersionAndLocale
PASS FetchAsync_RequestsAllFourDocumentsForBothLocales
PASS HttpTransportException_DoesNotExposeResponseBody
PASS RunAsync_DoesNotPublishWhenValidationFails
PASS RunAsync_IsIdempotentForAnIdenticalPatch
PASS RunAsync_ProfilesAndVersionsPublishToFourIndependentDirectories
PASS RunAsync_IdenticalOfficialLegacyRerunUsesVerifiedExistingDirectory
PASS RunAsync_IndexFailureReportsRecoverableUnindexedDirectory
PASS RunAsync_RollbackFailureReportsUncertainIndexStateForRecovery
PASS RunAsync_ChangedStandaloneManifestIdentityCannotReturnIdempotent
PASS RunAsync_ChangedStandaloneProvenanceCannotReturnIdempotent
PASS RunAsync_ChangedStandaloneProvenanceCannotReturnIdempotent
PASS RunAsync_RejectsSupplementedProfileWhenNoSupplementalSourceWasLoaded
PASS RunAsync_InvalidIconKeepsExistingOutputAndIndex
PASS RunAsync_CancellationKeepsExistingOutputAndIndex
PASS RunAsync_CancellationAfterIconResponseDoesNotPublish
PASS RunAsync_RefreshFailureReportsPublishedDirectoryForRecovery
PASS RunAsync_IsIdempotentWhenOnlyRetrievalTimeChanges
PASS RunAsync_RejectsDifferentContentForExistingPatch
PASS RunAsync_WritesContentAddressedPngAndUpdatesCatalogReference
PASS RunAsync_CleansStagingWhenCancelled
PASS Paths_RejectUnsafePatchSegments
PASS Paths_RejectUnsafePatchSegments
PASS DefaultSummaryNamesFullIdentityAndResolvedPath
PASS FailureStatusIncludesIdentityAndTargetWithoutExceptionBody
PASS CancellationStatusNamesFullIdentityAndTarget
PASS CancelledConfirmationDoesNotRunImportAndResetsConsent
PASS SuccessResetsConsentAndPassesSelectedProfile
PASS FailureResetsConsent
PASS CancellationResetsConsent
PASS OfficialOnlyNetworkSourceNeverRequestsCommunityDragonHost
PASS RejectedConfirmationNeverStartsNetworkSource
PASS RawSourcesAreSeparatedByVersionAndProfile
PASS PublishRejectsTamperedExistingCatalogEvenWhenManifestHashMatchesStagedHash
PASS FourIdentitiesForOnePatchCoexistAndExactUpsertReplacesOnlyOne
PASS CanonicalPathsSeparateVersionAndProfile
PASS VerifiedLegacyUpsertReplacesOldUnprofiledEntryWithoutRemovingSibling
PASS UnprofiledEntryCannotBeSilentlyReadAsOfficial
PASS SaveFailureRestoresExistingIndexBytesAndEntries
PASS IndexWriterRejectsMissingCatalogTarget
PASS UnsafeIdentitySegmentCannotBecomePath
PASS UnsafeIdentitySegmentCannotBecomePath
PASS UnsafeIdentitySegmentCannotBecomePath
PASS UnsafeIdentitySegmentCannotBecomePath
PASS OfficialSchemaOneWithPascalCaseSourcesResolvesExistingPath
PASS SupplementalSchemaOneIsAmbiguous
PASS EmbeddedSupplementalManifestCannotBeClaimedAsOfficial
PASS MissingEmbeddedManifestIsAmbiguous
PASS CommunityVersionInSchemaOneIsAmbiguous
PASS MissingManifestIsAmbiguous
PASS DifferentSourceVersionDoesNotClaimLegacy
PASS MalformedManifestIsAmbiguous
PASS ModifiedLegacyCatalogBytesCannotBeClassifiedCompatible
PASS CommittedSet18LegacyCatalogResolvesWithoutChangingItsBytes
PASS Serialize_SortsRecordsReferencesAndEffectKeys
PASS Deserialize_RoundTripsCanonicalCatalog
PASS Sha256Hex_ReturnsKnownVector
PASS Validate_BlocksDuplicateIdsAndMissingTraits
PASS Validate_ReportsInvalidNamesCostsItemReferencesAndIcons
Offline focused tests: 80 passed, 0 failed
```

### compile

Command: `dotnet build 'Library/SourceProfileNativeCompile/SourceProfileNativeCompile.csproj' --no-restore --nologo --verbosity quiet`

Exit: 0.

```text

빌드했습니다.
    경고 0개
    오류 0개

경과 시간: 00:00:00.75

워크로드 업데이트를 사용할 수 있습니다. 자세한 내용을 보려면 `dotnet workload list`을 실행하세요.
```

### diff

Command: `git diff --check -- Assets/TFTAnalyzer Assets/Plan`

Exit: 0.

```text
warning: in the working copy of 'Assets/Plan/Implementation/2026-09-17-tft-analyzer-editor-ui.md', LF will be replaced by CRLF the next time Git touches it
warning: in the working copy of 'Assets/Plan/Implementation/2026-09-17-tft-match-analytics.md', LF will be replaced by CRLF the next time Git touches it
warning: in the working copy of 'Assets/Plan/Implementation/2026-09-17-tft-static-data-pipeline.md', LF will be replaced by CRLF the next time Git touches it
warning: in the working copy of 'Assets/Plan/Implementation/TFTDataAnalyzerImplementationPlan.md', LF will be replaced by CRLF the next time Git touches it
warning: in the working copy of 'Assets/Plan/TFTDataPipeline.md', LF will be replaced by CRLF the next time Git touches it
warning: in the working copy of 'Assets/TFTAnalyzer/Core/Domain/PatchManifest.cs', LF will be replaced by CRLF the next time Git touches it
warning: in the working copy of 'Assets/TFTAnalyzer/Core/Serialization/CanonicalCatalogSerializer.cs', LF will be replaced by CRLF the next time Git touches it
warning: in the working copy of 'Assets/TFTAnalyzer/Data/Generated/CatalogIndex.asset', LF will be replaced by CRLF the next time Git touches it
warning: in the working copy of 'Assets/TFTAnalyzer/Editor/Ingestion/StaticCatalogPipeline.cs', LF will be replaced by CRLF the next time Git touches it
warning: in the working copy of 'Assets/TFTAnalyzer/Editor/Ingestion/StaticImportRequest.cs', LF will be replaced by CRLF the next time Git touches it
warning: in the working copy of 'Assets/TFTAnalyzer/Editor/Menu/StaticDataImportMenu.cs', LF will be replaced by CRLF the next time Git touches it
warning: in the working copy of 'Assets/TFTAnalyzer/Editor/Persistence/CatalogAssetWriter.cs', LF will be replaced by CRLF the next time Git touches it
warning: in the working copy of 'Assets/TFTAnalyzer/Editor/Persistence/CatalogIndex.cs', LF will be replaced by CRLF the next time Git touches it
warning: in the working copy of 'Assets/TFTAnalyzer/Editor/Persistence/StaticCatalogPaths.cs', LF will be replaced by CRLF the next time Git touches it
warning: in the working copy of 'Assets/TFTAnalyzer/Tests/EditMode/Ingestion/StaticCatalogPipelineTests.cs', LF will be replaced by CRLF the next time Git touches it
```

### preservation

Command: `Get-FileHash -Algorithm SHA256 -LiteralPath 'Assets/TFTAnalyzer/Data/Generated/TFTSet18/18.2/catalog.json','Assets/TFTAnalyzer/Data/Generated/TFTSet18/18.2/manifest.json','Assets/TFTAnalyzer/Data/Generated/TFTSet18/18.2/catalog.json.meta','Assets/TFTAnalyzer/Data/Generated/TFTSet18/18.2/manifest.json.meta','Assets/DefaultVolumeProfile.asset' | Select-Object Hash,Path | Format-List; git diff --name-only -- Assets/TFTAnalyzer/Data/Generated; git diff --cached --name-only; git branch --show-current; git rev-parse HEAD; git status --short`

Exit: 0.

```text

Hash : B306B524FAD37119EA88FE28F2F7241423EE997F06BDDC563890ADB35F0C27F9
Path : C:\Unity\TFTSimulator\Assets\TFTAnalyzer\Data\Generated\TFTSet18\18.2\catalog.json

Hash : FDDA8D453AC444350CD7785995135B59FB9319FFF0DA507ED6897E3FF93E0D71
Path : C:\Unity\TFTSimulator\Assets\TFTAnalyzer\Data\Generated\TFTSet18\18.2\manifest.json

Hash : E4FE14333934D51BB60A4241C5B034221913BA682978DE105FDB195E0304B265
Path : C:\Unity\TFTSimulator\Assets\TFTAnalyzer\Data\Generated\TFTSet18\18.2\catalog.json.meta

Hash : 6167B7251D766D9141760D862D385B5CE86E8BCD261B24B6877B7F9DC5B4AE74
Path : C:\Unity\TFTSimulator\Assets\TFTAnalyzer\Data\Generated\TFTSet18\18.2\manifest.json.meta

Hash : 4789B43834DD5EFB32EEC4C3B436BAF0895C9F8CF2A0624121DA8DD812EE984A
Path : C:\Unity\TFTSimulator\Assets\DefaultVolumeProfile.asset

warning: in the working copy of 'Assets/TFTAnalyzer/Data/Generated/CatalogIndex.asset', LF will be replaced by CRLF the next time Git touches it
codex/source-profile-identity
ec9d296ec877eda19a6167518df53b14471a15b7
 M Assets/DefaultVolumeProfile.asset
 M Assets/Plan/Implementation/2026-09-17-tft-analyzer-editor-ui.md
 M Assets/Plan/Implementation/2026-09-17-tft-match-analytics.md
 M Assets/Plan/Implementation/2026-09-17-tft-static-data-pipeline.md
 M Assets/Plan/Implementation/TFTDataAnalyzerImplementationPlan.md
 M Assets/Plan/TFTDataPipeline.md
 M Assets/TFTAnalyzer/Core/Domain/PatchManifest.cs
 M Assets/TFTAnalyzer/Core/Serialization/CanonicalCatalogSerializer.cs
 M Assets/TFTAnalyzer/Editor/Ingestion/StaticCatalogPipeline.cs
 M Assets/TFTAnalyzer/Editor/Ingestion/StaticImportRequest.cs
 M Assets/TFTAnalyzer/Editor/Menu/StaticDataImportMenu.cs
 M Assets/TFTAnalyzer/Editor/Persistence/CatalogAssetWriter.cs
 M Assets/TFTAnalyzer/Editor/Persistence/CatalogIndex.cs
 M Assets/TFTAnalyzer/Editor/Persistence/StaticCatalogPaths.cs
 M Assets/TFTAnalyzer/Tests/EditMode/Ingestion/StaticCatalogPipelineTests.cs
?? Assets/Plan/Workflow/
?? Assets/TFTAnalyzer/Core/Domain/CatalogIdentity.cs
?? Assets/TFTAnalyzer/Core/Domain/CatalogIdentity.cs.meta
?? Assets/TFTAnalyzer/Core/Domain/CatalogSourceProfile.cs
?? Assets/TFTAnalyzer/Core/Domain/CatalogSourceProfile.cs.meta
?? Assets/TFTAnalyzer/Editor/Menu/StaticImportPresentation.cs
?? Assets/TFTAnalyzer/Editor/Menu/StaticImportPresentation.cs.meta
?? Assets/TFTAnalyzer/Editor/Persistence/LegacyCatalogResolver.cs
?? Assets/TFTAnalyzer/Editor/Persistence/LegacyCatalogResolver.cs.meta
?? Assets/TFTAnalyzer/Tests/EditMode/Domain/CatalogIdentityTests.cs
?? Assets/TFTAnalyzer/Tests/EditMode/Domain/CatalogIdentityTests.cs.meta
?? Assets/TFTAnalyzer/Tests/EditMode/Ingestion/StaticImportPresentationTests.cs
?? Assets/TFTAnalyzer/Tests/EditMode/Ingestion/StaticImportPresentationTests.cs.meta
?? Assets/TFTAnalyzer/Tests/EditMode/Persistence.meta
?? Assets/TFTAnalyzer/Tests/EditMode/Persistence/
?? Assets/TFTAnalyzer/Tests/Fixtures/Static/legacy-official-manifest.json
?? Assets/TFTAnalyzer/Tests/Fixtures/Static/legacy-official-manifest.json.meta
```
