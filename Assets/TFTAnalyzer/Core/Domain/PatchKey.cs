using System;

namespace TFTAnalyzer.Domain
{
    public sealed class PatchKey
    {
        public string SetId { get; }
        public int SetNumber { get; }
        public string PatchId { get; }
        public string SourceGameVersion { get; }

        public PatchKey(string setId, int setNumber, string patchId, string sourceGameVersion)
        {
            SetId = DomainValues.RequireStable(setId, nameof(setId));
            if (setNumber <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(setNumber), "Set number must be positive.");
            }

            SetNumber = setNumber;
            PatchId = DomainValues.RequireStable(patchId, nameof(patchId));
            SourceGameVersion = DomainValues.RequireStable(sourceGameVersion, nameof(sourceGameVersion));
        }
    }
}
