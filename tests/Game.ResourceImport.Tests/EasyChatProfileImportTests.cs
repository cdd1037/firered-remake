namespace FireRed.Tests.ResourceImport;

[TestClass, TestCategory("ResourceImport")]
public sealed class EasyChatProfileImportTests
{
    [TestMethod]
    public Task ProfileAndMailShareSourceTemplatesVocabularyAndSettledPixels() =>
        PythonResourceCheck.Run("scripts/checks/test-easy-chat-profile-import.py", timeout: TimeSpan.FromMinutes(1));

    [TestMethod]
    public Task SharedVocabularyAndEditorContractsRegenerateExactly() =>
        PythonResourceCheck.Run("scripts/checks/test-mail-composer.py", timeout: TimeSpan.FromMinutes(1));
}
