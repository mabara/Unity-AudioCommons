using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

[Serializable]
public class AudioCommonsContentLink
{
    public string type;
    public string locator;
    public int audioChannelNumber;
    public int bitRate;
    public string hasAudioEncodingFormat;
    public int sampleRate;
    public int sampleSize;
}

[Serializable]
public class AudioCommonsMemberContent
{
    public List<AudioCommonsContentLink> availableAs;
    public string title;
    public string description;
    public string license;
}

[Serializable]
public class AudioCommonsMember
{
    public AudioCommonsMemberContent content;
}

[Serializable]
public class AudioCommonsResult
{
    public string type;
    public List<AudioCommonsMember> members;
}

[Serializable]
public class AudioCommonsResponse
{
    public string id;
    public string query;
    public string actionStatus;
    public List<string> errors;
    public List<AudioCommonsResult> results;
}


public class AudioCommonsWindow : EditorWindow
{
    [MenuItem("Window/Audio Commons")]
    public static void ShowWindow()
    {
        //Show existing window instance. If one doesn't exist, make one.
        GetWindow(typeof(AudioCommonsWindow));
    }

    private string m_SearchString;
    private AudioCommonsResponse m_AudioCommonsResponse;

    [SerializeField]
    private Vector2 m_ResultsScroll;

    private int m_CurrentPage = 1;
    

    private void OnGUI()
    {
        GUILayout.BeginHorizontal(GUI.skin.FindStyle("Toolbar"));
        EditorGUI.BeginChangeCheck();
        m_SearchString = GUILayout.TextField(m_SearchString, GUI.skin.FindStyle("ToolbarSeachTextField"));
        if (EditorGUI.EndChangeCheck())
            m_CurrentPage = 1;
        
        if (GUILayout.Button("", GUI.skin.FindStyle("ToolbarSeachCancelButton")))
        {
            // Remove focus if cleared
            m_SearchString = "";
            GUI.FocusControl(null);
        }
        GUILayout.EndHorizontal();

        if (GUILayout.Button("Search"))
        {
            m_AudioCommonsResponse = QueryAudioCommonsApi(10, m_CurrentPage);
        }

        if (m_AudioCommonsResponse != null)
        {
            m_ResultsScroll = EditorGUILayout.BeginScrollView(m_ResultsScroll);
            var results = m_AudioCommonsResponse.results;
            EditorGUILayout.LabelField($"Number of Audio Clips found: {results.Sum(result => result.members.Count)}");
            foreach (var result in results)
            {
                foreach (var member in result.members)
                {
                    GUILayout.BeginHorizontal(EditorStyles.helpBox);
                    {
                        GUILayout.BeginVertical();
                        {
                            EditorGUILayout.LabelField($"Title: {member.content.title}");
                            EditorGUILayout.LabelField($"Desc: {member.content.description}");
                        }
                        GUILayout.EndVertical();

                        if (GUILayout.Button("Import"))
                        {
                            DownloadAndImportAudio(member.content.title, member.content.availableAs[0].hasAudioEncodingFormat, member.content.availableAs[0].locator);
                        }
                    }
                    GUILayout.EndHorizontal();
                }
            }
            EditorGUILayout.EndScrollView();

            GUILayout.BeginHorizontal(EditorStyles.helpBox);
            EditorGUILayout.LabelField($"Page: {m_CurrentPage}");

            if (GUILayout.Button("<"))
            {
                if (m_CurrentPage > 1)
                {
                    m_CurrentPage--;
                    m_AudioCommonsResponse = QueryAudioCommonsApi(10, m_CurrentPage);
                }
            }

            if (GUILayout.Button(">"))
            {
                m_CurrentPage++;
                m_AudioCommonsResponse = QueryAudioCommonsApi(10, m_CurrentPage);
            }
            GUILayout.EndHorizontal();
        }
    }

    private AudioCommonsResponse QueryAudioCommonsApi(int limit, int page)
    {
        HttpClient client = new HttpClient();

        var request = client.GetAsync(
            $"https://m2.audiocommons.org/api/audioclips/search?pattern={m_SearchString}&limit={limit}&page={page}&source=freesound%2Cjamendo%2Ceuropeana");
        request.Wait();
        var response = request.Result;
        response.EnsureSuccessStatusCode();

        var jsonRequest = response.Content.ReadAsStringAsync();
        jsonRequest.Wait();
        var jsonResult = jsonRequest.Result;

        return JsonUtility.FromJson<AudioCommonsResponse>(jsonResult);
    }

    private void DownloadAndImportAudio(string title, string encoding, string url)
    {
        Regex rgx = new Regex("[^a-zA-Z0-9 -]");
        encoding = rgx.Replace(encoding, "");
        
        HttpClient client = new HttpClient();

        var request = client.GetAsync(url);
        request.Wait();
        var response = request.Result;
        response.EnsureSuccessStatusCode();

        if (!Directory.Exists("Assets/AudioCommonSounds"))
            Directory.CreateDirectory("Assets/AudioCommonSounds");

        var assetPath = $"Assets/AudioCommonSounds/{title}.{encoding}";

        using(var fs = new FileStream(
            assetPath,
            FileMode.CreateNew))
        {
            response.Content.CopyToAsync(fs).Wait();
        }
        
        AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
    }
}