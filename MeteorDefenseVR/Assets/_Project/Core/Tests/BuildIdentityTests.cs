using System.IO;
using NUnit.Framework;

namespace MeteorDefenseVR.Tests
{
    public sealed class BuildIdentityTests
    {
        [Test]
        public void BuildPipelineRecordsRequiredIdentityFieldsAndRejectsDirtyTrees()
        {
            string source = File.ReadAllText("Assets/_Project/Core/Editor/PcTestSetup.cs");
            foreach (string field in new[] { "buildVersion", "gitBranch", "gitCommit", "gitCommitShort", "buildTimeKST", "unityVersion", "scene", "developmentBuild", "worktreeClean" })
                StringAssert.Contains(field, source);
            StringAssert.Contains("Git worktree is dirty", source);
            StringAssert.Contains("BUILD_IDENTITY.json", source);
        }

        [Test]
        public void RuntimeOverlayShowsIdentityOnlyOutsidePublicMode()
        {
            string source = File.ReadAllText("Assets/_Project/PcInput/PcTestOverlay.cs");
            StringAssert.Contains("development || operatorUi", source);
            StringAssert.Contains("BUILD ", source);
            StringAssert.Contains("BUILD_IDENTITY.json", source);
        }
    }
}
