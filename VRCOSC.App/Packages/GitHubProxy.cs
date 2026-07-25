// Copyright (c) VolcanicArts. Licensed under the GPL-3.0 License.
// See the LICENSE file in the repository root for full license text.

using Octokit;

namespace VRCOSC.App.Packages;

public static class GitHubProxy
{
    private const string client_id = "70c1b7af05288131463c";
    private const string client_secret = "";

    public static readonly GitHubClient Client = createClient();

    private static GitHubClient createClient()
    {
        var client = new GitHubClient(new ProductHeaderValue("VRCOSC"));
        if (!string.IsNullOrEmpty(client_secret))
        {
            client.Credentials = new Credentials(client_id, client_secret, AuthenticationType.Basic);
        }
        return client;
    }
}