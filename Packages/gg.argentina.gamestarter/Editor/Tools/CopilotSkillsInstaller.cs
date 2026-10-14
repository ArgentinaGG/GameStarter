using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;

namespace gg.argentina.gamestarter.Editor
{
    public static class CopilotSkillsInstaller
    {
        private const string RepositoryApi = "https://api.github.com/repos/mattpocock/skills";
        private const string MainCommitUrl = RepositoryApi + "/commits/main";
        private const string CommitArchiveUrl = "https://codeload.github.com/mattpocock/skills/zip/";
        private static readonly SkillDefinition[] Skills =
        {
            new SkillDefinition("grill-me", "skills/productivity/grill-me"),
            new SkillDefinition("grill-with-docs", "skills/engineering/grill-with-docs"),
            new SkillDefinition("grilling", "skills/productivity/grilling"),
            new SkillDefinition("domain-modeling", "skills/engineering/domain-modeling")
        };

        private static UnityWebRequest request;
        private static RequestStage requestStage;
        private static bool isInstalling;
        private static string commitSha;

        public static event Action StatusChanged;

        public static bool IsInstalling => isInstalling;
        public static string Status { get; private set; }
        public static bool LastOperationFailed { get; private set; }

        public static bool IsInstalled
        {
            get
            {
                try
                {
                    string skillsRoot = Path.Combine(GetAgentRoot(), "skills");
                    foreach (SkillDefinition skill in Skills)
                    {
                        if (!File.Exists(Path.Combine(skillsRoot, skill.Name, "SKILL.md")))
                            return false;
                    }

                    return true;
                }
                catch
                {
                    return false;
                }
            }
        }

        public static void InstallOrUpdate()
        {
            if (isInstalling)
                return;

            bool replacingExistingFiles;
            try
            {
                replacingExistingFiles = HasExistingSkillFiles();
            }
            catch (Exception exception)
            {
                Complete(false, $"Could not inspect the project skills folder: {exception.Message}");
                return;
            }

            if (replacingExistingFiles && !EditorUtility.DisplayDialog(
                    "Update Copilot Skills",
                    "This replaces the installed grill-me, grill-with-docs, grilling, and domain-modeling folders with files from the latest main commit. Other skills are left unchanged.",
                    "Update",
                    "Cancel"))
                return;

            isInstalling = true;
            LastOperationFailed = false;
            EditorApplication.update += Update;
            try
            {
                StartRequest(MainCommitUrl, RequestStage.ResolveCommit);
            }
            catch (Exception exception)
            {
                Complete(false, $"Could not start the GitHub request: {exception.Message}");
            }
        }

        private static void Update()
        {
            if (request == null || !request.isDone)
                return;

            UnityWebRequest completedRequest = request;
            request = null;
            if (completedRequest.result != UnityWebRequest.Result.Success)
            {
                string error = completedRequest.error;
                long responseCode = completedRequest.responseCode;
                completedRequest.Dispose();
                Complete(false, $"GitHub request failed ({responseCode}): {error}");
                return;
            }

            try
            {
                if (requestStage == RequestStage.ResolveCommit)
                {
                    string response = completedRequest.downloadHandler.text;
                    completedRequest.Dispose();
                    GitHubCommitResponse commitResponse = JsonUtility.FromJson<GitHubCommitResponse>(response);
                    if (commitResponse == null || string.IsNullOrWhiteSpace(commitResponse.sha))
                        throw new InvalidDataException("GitHub did not return a commit SHA for main.");

                    commitSha = commitResponse.sha;
                    StartRequest(CommitArchiveUrl + commitSha, RequestStage.DownloadArchive);
                }
                else
                {
                    byte[] archiveData = completedRequest.downloadHandler.data;
                    completedRequest.Dispose();
                    InstallArchive(archiveData);
                    Complete(true, $"Installed the selected skills from main at commit {commitSha}.");
                }
            }
            catch (Exception exception)
            {
                completedRequest.Dispose();
                Complete(false, $"Skill installation failed: {exception.Message}");
            }
        }

        private static void StartRequest(string url, RequestStage stage)
        {
            requestStage = stage;
            Status = stage == RequestStage.ResolveCommit
                ? "Checking the latest main commit on GitHub..."
                : "Downloading the selected skills...";
            NotifyStatusChanged();

            request = UnityWebRequest.Get(url);
            if (stage == RequestStage.ResolveCommit)
                request.SetRequestHeader("Accept", "application/vnd.github+json");

            request.SendWebRequest();
        }

        private static void InstallArchive(byte[] archiveData)
        {
            if (archiveData == null || archiveData.Length == 0)
                throw new InvalidDataException("GitHub returned an empty skills archive.");

            string agentRoot = GetAgentRoot();
            Directory.CreateDirectory(agentRoot);
            string stagingRoot = Path.Combine(agentRoot, ".mattpocock-staging-" + Guid.NewGuid().ToString("N"));
            string stagedSkillsRoot = Path.Combine(stagingRoot, "skills");
            Directory.CreateDirectory(stagedSkillsRoot);

            try
            {
                ExtractSelectedSkills(archiveData, stagedSkillsRoot);
                Status = "Installing skills into the project...";
                NotifyStatusChanged();
                ReplaceSelectedSkills(agentRoot, stagingRoot);
            }
            finally
            {
                TryDeleteDirectory(stagingRoot);
            }
        }

        private static void ExtractSelectedSkills(byte[] archiveData, string stagedSkillsRoot)
        {
            var extractedSkills = new HashSet<string>(StringComparer.Ordinal);
            using (var archiveStream = new MemoryStream(archiveData))
            using (var archive = new ZipArchive(archiveStream, ZipArchiveMode.Read))
            {
                foreach (ZipArchiveEntry entry in archive.Entries)
                {
                    string entryPath = entry.FullName.Replace('\\', '/');
                    int rootSeparator = entryPath.IndexOf('/');
                    if (rootSeparator < 0 || rootSeparator == entryPath.Length - 1)
                        continue;

                    string repositoryPath = entryPath.Substring(rootSeparator + 1);
                    foreach (SkillDefinition skill in Skills)
                    {
                        string sourcePrefix = skill.RepositoryPath + "/";
                        if (!repositoryPath.StartsWith(sourcePrefix, StringComparison.Ordinal))
                            continue;

                        string relativePath = repositoryPath.Substring(sourcePrefix.Length);
                        if (string.IsNullOrEmpty(relativePath) || entry.Name.Length == 0)
                            continue;
                        if (relativePath.StartsWith("agents/", StringComparison.OrdinalIgnoreCase))
                            continue;

                        string skillRoot = Path.Combine(stagedSkillsRoot, skill.Name);
                        string destinationPath = GetSafePath(skillRoot, relativePath);
                        string directory = Path.GetDirectoryName(destinationPath);
                        if (!string.IsNullOrEmpty(directory))
                            Directory.CreateDirectory(directory);

                        using (Stream source = entry.Open())
                        using (FileStream destination = File.Create(destinationPath))
                            source.CopyTo(destination);

                        extractedSkills.Add(skill.Name);
                    }
                }
            }

            foreach (SkillDefinition skill in Skills)
            {
                string skillFile = Path.Combine(stagedSkillsRoot, skill.Name, "SKILL.md");
                if (!extractedSkills.Contains(skill.Name) || !File.Exists(skillFile))
                    throw new InvalidDataException($"The GitHub archive did not contain the expected {skill.Name} skill files.");
            }
        }

        private static void ReplaceSelectedSkills(string agentRoot, string stagingRoot)
        {
            string destinationSkillsRoot = Path.Combine(agentRoot, "skills");
            string stagedSkillsRoot = Path.Combine(stagingRoot, "skills");
            string backupRoot = Path.Combine(agentRoot, ".mattpocock-backup-" + Guid.NewGuid().ToString("N"));
            var backedUpSkills = new List<string>();
            var installedSkills = new List<string>();
            bool rollbackSucceeded = false;

            Directory.CreateDirectory(destinationSkillsRoot);
            Directory.CreateDirectory(backupRoot);

            try
            {
                foreach (SkillDefinition skill in Skills)
                {
                    string destination = Path.Combine(destinationSkillsRoot, skill.Name);
                    string staged = Path.Combine(stagedSkillsRoot, skill.Name);
                    string backup = Path.Combine(backupRoot, skill.Name);

                    if (Directory.Exists(destination))
                    {
                        Directory.Move(destination, backup);
                        backedUpSkills.Add(skill.Name);
                    }

                    Directory.Move(staged, destination);
                    installedSkills.Add(skill.Name);
                }
            }
            catch (Exception installException)
            {
                try
                {
                    RollBackInstall(destinationSkillsRoot, backupRoot, installedSkills, backedUpSkills);
                    rollbackSucceeded = true;
                }
                catch (Exception rollbackException)
                {
                    throw new IOException(
                        $"Installation failed and rollback was incomplete. Backup files were retained at {backupRoot}.",
                        new AggregateException(installException, rollbackException));
                }

                throw;
            }
            finally
            {
                if (rollbackSucceeded)
                    TryDeleteDirectory(backupRoot);
            }

            TryDeleteDirectory(backupRoot);
        }

        private static void RollBackInstall(
            string destinationSkillsRoot,
            string backupRoot,
            List<string> installedSkills,
            List<string> backedUpSkills)
        {
            for (int i = installedSkills.Count - 1; i >= 0; i--)
            {
                string destination = Path.Combine(destinationSkillsRoot, installedSkills[i]);
                if (Directory.Exists(destination))
                    Directory.Delete(destination, true);
            }

            for (int i = backedUpSkills.Count - 1; i >= 0; i--)
            {
                string backup = Path.Combine(backupRoot, backedUpSkills[i]);
                string destination = Path.Combine(destinationSkillsRoot, backedUpSkills[i]);
                if (Directory.Exists(backup))
                    Directory.Move(backup, destination);
            }
        }

        private static bool HasExistingSkillFiles()
        {
            string skillsRoot = Path.Combine(GetAgentRoot(), "skills");
            foreach (SkillDefinition skill in Skills)
            {
                if (Directory.Exists(Path.Combine(skillsRoot, skill.Name)))
                    return true;
            }

            return false;
        }

        private static string GetAgentRoot()
        {
            DirectoryInfo projectRoot = Directory.GetParent(Application.dataPath);
            if (projectRoot == null)
                throw new InvalidOperationException("Could not determine the Unity project root.");

            return Path.Combine(projectRoot.FullName, ".github");
        }

        private static string GetSafePath(string root, string relativePath)
        {
            string fullRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            string fullPath = Path.GetFullPath(Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar)));
            if (!fullPath.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("The GitHub archive contains an invalid file path.");

            return fullPath;
        }

        private static void Complete(bool succeeded, string status)
        {
            if (request != null)
            {
                request.Dispose();
                request = null;
            }

            EditorApplication.update -= Update;
            isInstalling = false;
            LastOperationFailed = !succeeded;
            Status = status;
            NotifyStatusChanged();
        }

        private static void NotifyStatusChanged()
        {
            StatusChanged?.Invoke();
        }

        private static void TryDeleteDirectory(string directory)
        {
            if (!Directory.Exists(directory))
                return;

            try
            {
                Directory.Delete(directory, true);
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"Could not remove temporary skills directory '{directory}': {exception.Message}");
            }
        }

        [Serializable]
        private sealed class GitHubCommitResponse
        {
            public string sha;
        }

        private sealed class SkillDefinition
        {
            public SkillDefinition(string name, string repositoryPath)
            {
                Name = name;
                RepositoryPath = repositoryPath;
            }

            public string Name { get; }
            public string RepositoryPath { get; }
        }

        private enum RequestStage
        {
            ResolveCommit,
            DownloadArchive
        }
    }
}
