using System;
using System.Collections.Generic;

namespace TFTAnalyzer.Editor.Ingestion.DataDragon
{
    public static class DataDragonEndpoints
    {
        public const string Versions = "https://ddragon.leagueoflegends.com/api/versions.json";

        private static readonly IReadOnlyDictionary<DataDragonDocumentKind, string> DocumentNames =
            new Dictionary<DataDragonDocumentKind, string>
            {
                [DataDragonDocumentKind.Champions] = "tft-champion.json",
                [DataDragonDocumentKind.Traits] = "tft-trait.json",
                [DataDragonDocumentKind.Items] = "tft-item.json",
                [DataDragonDocumentKind.Augments] = "tft-augments.json"
            };

        private static readonly IReadOnlyDictionary<DataDragonDocumentKind, string> ImageFolders =
            new Dictionary<DataDragonDocumentKind, string>
            {
                [DataDragonDocumentKind.Champions] = "tft-champion",
                [DataDragonDocumentKind.Traits] = "tft-trait",
                [DataDragonDocumentKind.Items] = "tft-item",
                [DataDragonDocumentKind.Augments] = "tft-augment"
            };

        public static string Document(
            string version,
            string locale,
            DataDragonDocumentKind kind)
        {
            if (string.IsNullOrWhiteSpace(version))
            {
                throw new ArgumentException("Version cannot be blank.", nameof(version));
            }

            if (string.IsNullOrWhiteSpace(locale))
            {
                throw new ArgumentException("Locale cannot be blank.", nameof(locale));
            }

            return $"https://ddragon.leagueoflegends.com/cdn/{Uri.EscapeDataString(version)}/data/" +
                   $"{Uri.EscapeDataString(locale)}/{DocumentNames[kind]}";
        }

        public static string Image(string version, DataDragonDocumentKind kind, string filename)
        {
            if (string.IsNullOrWhiteSpace(filename))
            {
                return "";
            }

            return $"https://ddragon.leagueoflegends.com/cdn/{Uri.EscapeDataString(version)}/img/" +
                   $"{ImageFolders[kind]}/{Uri.EscapeDataString(filename)}";
        }
    }
}
