import sys
import re

def append_to_class(file_path, new_methods):
    with open(file_path, 'r') as f:
        content = f.read()
    
    # Strip any trailing whitespace
    content = content.rstrip()
    
    # Remove the last '}'
    if content.endswith('}'):
        content = content[:-1]
    
    # Append the new methods and close the class
    content += '\n' + new_methods + '\n}\n'
    
    with open(file_path, 'w') as f:
        f.write(content)

# 1. ChatHubTests.cs
chathub_tests = """
    [Test]
    public async Task AskQuestion_NullOrEmptyRepoId_ReturnsEarly()
    {
        await _hub.AskQuestion("", "mode1", "question?");
        _clientProxyMock.Verify(c => c.SendCoreAsync(It.IsAny<string>(), It.IsAny<object[]>(), default), Times.Never);
    }

    [Test]
    public async Task AskQuestion_NullClients_ReturnsEarly()
    {
        _hub.Clients = null;
        await _hub.AskQuestion("repo1", "mode1", "question?");
        _aiServiceMock.Verify(a => a.GenerateStructuredDocsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }
"""
append_to_class('Unit/Hubs/ChatHubTests.cs', chathub_tests)

# 2. AiClientServiceTests.cs
ai_tests = """
    [Test]
    public void GenerateStructuredDocsAsync_NullOrEmptyCode_ThrowsArgumentException()
    {
        Assert.ThrowsAsync<ArgumentException>(async () => await _service.GenerateStructuredDocsAsync("", "owner", "repo"));
    }
"""
append_to_class('Unit/Services/AiClientServiceTests.cs', ai_tests)

# 3. GitHubServiceTests.cs
github_tests = """
    [Test]
    public async Task SearchReposAsync_NullOrEmptyQuery_ReturnsEmptyList()
    {
        var result = await _service.SearchReposAsync("");
        result.Should().BeEmpty();
    }
"""
append_to_class('Unit/Services/GitHubServiceTests.cs', github_tests)

print("Tests appended successfully.")
