using Code.Artifacts;
using UnityEngine;

namespace Code.Stages
{
    /// <summary>
    /// 스테이지 보상 후보 유물. ID는 에셋 GUID라 이름을 바꿔도 보유 기록이 이어진다.
    /// 설명이 비어 있으면 유물 원래 설명을 쓰고, 스테이지에서 뜻이 달라진 유물만 따로 적는다.
    /// </summary>
    [System.Serializable]
    public sealed class StageArtifact
    {
        [field: SerializeField] public string Id { get; private set; }
        [field: SerializeField] public ArtifactDataSO Artifact { get; private set; }
        [field: SerializeField, TextArea] public string StageDescription { get; private set; }
        public string Label => Artifact != null ? Artifact.DisplayName : Id;
        public string Description => !string.IsNullOrEmpty(StageDescription) ? StageDescription
            : Artifact != null ? Artifact.Description : string.Empty;

        public StageArtifact(string id, ArtifactDataSO artifact, string stageDescription)
        {
            Id = id;
            Artifact = artifact;
            StageDescription = stageDescription;
        }
    }
}
