using System;

namespace Nomad
{
    public struct FCXMapInfo
    {
        public struct SMapId
        {
            public Guid Id;
            public int Type;
        }

        public string Creator;
        public string Editor;

        public string Name;

        public DateTime TimeCreated;
        public DateTime TimeModified;

        public Guid VersionId;

        public int GameModes;
        public int IGEVersion;
        public int ContentFlags;

        public long GameModeId;
        public long ObjectiveId;

        public string ContentId;
        public int ContentPatchVersionId;

        public int FileLocation;

        public Guid CreatorProfile;
        public Guid EditorProfile;

        public int MapEditCount;

        private bool IsValid;
        private bool IScreenshotSet;
        private bool IsMadeByUbi;
        private bool IsOriginallyCreatedByUbi;

        public SMapId MapId;
        
        public void Deserialize(NomadObject meta)
        {
            var mup = new MapDataUnpacker(meta);

            Creator = mup.GetString("OriginalCreatorName");
            Editor = mup.GetString("LatestCreatorName");
            Name = mup.GetString("MapName");

            TimeModified = mup.GetDateTime("LatestModificationTime");
            TimeCreated = mup.GetDateTime("OriginalCreationTime");

            VersionId = mup.GetUID("VersionId");

            GameModes = mup.GetInt("GameModes");
            IGEVersion = mup.GetInt("IGEVersion");
            ContentFlags = mup.GetInt("ContentFlags");

            IsValid = mup.GetBool("bIsValid");
            IScreenshotSet = mup.GetBool("bIScreenshotSet");
            IsMadeByUbi = mup.GetBool("bIsMadeByUbi");
            IsOriginallyCreatedByUbi = mup.GetBool("bIsOriginallyCreatedByUbi");

            GameModeId = mup.GetLong("GameModeId");
            ObjectiveId = mup.GetLong("ObjectiveId");

            ContentId = mup.GetString("ContentId");
            ContentPatchVersionId = mup.GetInt("ContentPatchVersionId");

            FileLocation = mup.GetInt("FileLocation");

            EditorProfile = mup.GetUID("CreatorProfileId");
            CreatorProfile = mup.GetUID("OriginalCreatorProfileId");

            var idup = mup.GetObject("MapId");

            MapId = new SMapId() {
                Id = idup.GetUID("id"),
                Type = idup.GetInt("idType"),
            };
        }
    }
}
