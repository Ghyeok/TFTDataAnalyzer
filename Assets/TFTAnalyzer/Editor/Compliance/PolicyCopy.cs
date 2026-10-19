namespace TFTAnalyzer.Editor.Compliance
{
    public static class PolicyCopy
    {
        internal const string GeneralNotice = "TFT Data Analyzer isn't endorsed by Riot Games and doesn't reflect the views or opinions of Riot Games or anyone officially involved in producing or managing Riot Games properties. Riot Games, and all associated properties are trademarks or registered trademarks of Riot Games, Inc.";
        internal const string LegalNotice = "TFT Data Analyzer was created under Riot Games' \"Legal Jibber Jabber\" policy using assets owned by Riot Games.  Riot Games does not endorse or sponsor this project.";
        public static string[] Urls => new[]
        {
            "https://developer.riotgames.com/docs/tft",
            "https://support-developer.riotgames.com/hc/en-us/articles/22698591841939-General-Policies",
            "https://www.riotgames.com/en/legal"
        };

        internal static string StateText(ComplianceSettingsReadResult read)
        {
            if (read.State == ComplianceReadState.Invalid) return "설정이 올바르지 않아 배포가 차단됩니다. Project Settings에서 다시 확인하고 저장해 주세요.";
            if (read.State == ComplianceReadState.Unavailable) return "설정을 읽을 수 없어 배포가 차단됩니다. Project Settings에서 다시 확인하고 저장해 주세요.";
            if (read.State == ComplianceReadState.Missing) return "등록 미확인 · 저장된 확인 기록이 없습니다.";
            var settings = read.Settings;
            var status = settings.registrationStatus == "ApprovedOrAcknowledged" ? "등록 확인 기록" :
                settings.registrationStatus == "Withdrawn" ? "등록 철회" : "등록 미확인";
            return status + "\n정책 검토일: " + (string.IsNullOrEmpty(settings.policyReviewedOn) ? "미기록" : settings.policyReviewedOn) +
                "\nPortal 상태 확인 시각(UTC): " + (string.IsNullOrEmpty(settings.acknowledgedAt) ? "미기록" : settings.acknowledgedAt) +
                "\n사용자가 기록한 로컬 상태이며 Riot의 온라인 승인을 검증한 결과가 아닙니다.";
        }
    }
}
