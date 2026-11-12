using System;
using System.Collections.Generic;
using System.Linq;
using TFTAnalyzer.Editor.Persistence;
using UnityEngine.UIElements;

namespace TFTAnalyzer.Editor.UI.Collection
{
    internal sealed class IncompleteRecordsTab : IDisposable
    {
        private readonly VisualElement host, list, detail;
        private readonly Label status;
        private readonly Button refresh, delete;
        private readonly Func<IReadOnlyList<IncompleteStoredRecord>> read;
        private readonly Func<string, bool> remove;
        private readonly Func<string, bool> confirm;
        private IReadOnlyList<IncompleteStoredRecord> records = Array.Empty<IncompleteStoredRecord>();
        private string selected;
        private bool disposed;

        internal IncompleteRecordsTab(VisualElement host, Func<IReadOnlyList<IncompleteStoredRecord>> read,
            Func<string, bool> remove, Func<string, bool> confirm)
        {
            this.host = host ?? throw new ArgumentNullException(nameof(host));
            this.read = read ?? throw new ArgumentNullException(nameof(read));
            this.remove = remove ?? throw new ArgumentNullException(nameof(remove));
            this.confirm = confirm ?? throw new ArgumentNullException(nameof(confirm));
            list = host.Q("incomplete-list"); detail = host.Q("incomplete-detail");
            status = host.Q<Label>("incomplete-status");
            refresh = host.Q<Button>("incomplete-refresh"); delete = host.Q<Button>("incomplete-delete");
            refresh.clicked += Refresh; delete.clicked += Delete;
            Invalidate();
        }

        internal void Invalidate()
        {
            if (disposed) return;
            selected = null; records = Array.Empty<IncompleteStoredRecord>();
            list.Clear(); detail.Clear(); delete.SetEnabled(false);
            status.text = "기록을 확인하려면 새로 확인을 눌러 주세요.";
        }

        internal void Refresh()
        {
            if (disposed) return;
            selected = null; detail.Clear(); delete.SetEnabled(false);
            try
            {
                records = read() ?? throw new InvalidOperationException();
                RenderList();
                status.text = records.Count == 0 ? "별도 보관된 기록이 없습니다." : $"별도 보관 기록 {records.Count}건";
            }
            catch { records = Array.Empty<IncompleteStoredRecord>(); list.Clear(); status.text = "기록을 불러오지 못했습니다. 다시 확인해 주세요."; }
        }

        private void RenderList()
        {
            list.Clear();
            for (var i = 0; i < records.Count; i++)
            {
                var record = records[i];
                var button = new Button(() => Select(record.Handle)) { name = "incomplete-record-" + i,
                    text = $"기록 {i + 1} · " + (record.IsCorrupt ? "손상 · 확인 필요" : "불완전 수신 · 추가 확인 필요 · 정상 통계 제외") };
                list.Add(button);
            }
        }

        private void Select(string handle)
        {
            if (disposed) return;
            selected = null; detail.Clear(); delete.SetEnabled(false);
            try
            {
                records = read() ?? throw new InvalidOperationException();
                var record = records.FirstOrDefault(x => x.Handle == handle);
                RenderList();
                if (record == null) { status.text = "선택한 기록이 더 이상 없습니다."; return; }
                selected = record.Handle;
                delete.SetEnabled(true);
                status.text = "선택한 불완전 수신 기록 · 추가 확인 필요 · 정상 통계 제외";
                if (record.IsCorrupt) detail.Add(new Label("손상된 기록입니다. 상세를 표시할 수 없습니다."));
                else new IncompleteSyntheticCandidateTab(detail).ShowRecord(record);
            }
            catch { records = Array.Empty<IncompleteStoredRecord>(); list.Clear(); status.text = "기록을 불러오지 못했습니다. 다시 확인해 주세요."; }
        }

        private void Delete()
        {
            if (disposed || selected == null) return;
            var handle = selected;
            try
            {
                if (!confirm("선택한 불완전 수신 기록을 삭제하시겠습니까?")) return;
                var removed = remove(handle);
                Refresh();
                if (!removed) status.text = "선택한 기록이 이미 없습니다. 목록을 다시 확인했습니다.";
            }
            catch { selected = null; detail.Clear(); delete.SetEnabled(false); status.text = "기록을 삭제하지 못했습니다. 다시 확인해 주세요."; }
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true; refresh.clicked -= Refresh; delete.clicked -= Delete;
            list.Clear(); detail.Clear(); selected = null;
        }
    }
}
