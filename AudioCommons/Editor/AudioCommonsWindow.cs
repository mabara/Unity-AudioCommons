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
    public string author;
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
        var window = GetWindow(typeof(AudioCommonsWindow));
        window.titleContent = new GUIContent("Audio Commons Importer");
    }

    private string m_SearchString;
    private AudioCommonsResponse m_AudioCommonsResponse;

    [SerializeField]
    private Vector2 m_ResultsScroll;
    
    [SerializeField]
    private List<int> m_DownloadIndicies = new List<int>();

    private int m_CurrentPage = 1;

    private void Reset()
    {
        m_CurrentPage = 1;
        m_ResultsScroll = Vector2.zero;
        m_DownloadIndicies.Clear();
    }

    private void OnGUI()
    {
        GUILayout.BeginHorizontal(GUI.skin.FindStyle("Toolbar"));

        m_SearchString = GUILayout.TextField(m_SearchString, GUI.skin.FindStyle("ToolbarSeachTextField"));

        if (GUILayout.Button("", GUI.skin.FindStyle("ToolbarSeachCancelButton")))
        {
            // Remove focus if cleared
            m_SearchString = "";
            GUI.FocusControl(null);
        }
        GUILayout.EndHorizontal();

        if (GUILayout.Button("Search"))
        {
            Reset();
            QueryAudioCommonsApi(10, m_CurrentPage);
        }

        if (m_AudioCommonsResponse != null)
        {
            var results = m_AudioCommonsResponse.results;
            m_ResultsScroll = EditorGUILayout.BeginScrollView(m_ResultsScroll);

            var downloadIndex = 0;
            foreach (var result in results)
            {
                foreach (var member in result.members)
                {
                    GUILayout.BeginVertical(EditorStyles.helpBox);
                    {
                        EditorGUILayout.LabelField($"Title: {member.content.title}");
                        
                        if(!string.IsNullOrEmpty(member.content.description))
                            EditorGUILayout.LabelField($"Desc: {member.content.description}");
                        
                        if(!string.IsNullOrEmpty(member.content.author))
                            EditorGUILayout.LabelField($"Author: {member.content.author}");
                        
                        EditorGUILayout.LabelField($"License: {member.content.license}");
                        
                        GUILayout.BeginHorizontal();
                        {
                            var audioFiles = new List<string>(member.content.availableAs.Count);
                            foreach (var content in member.content.availableAs)
                                audioFiles.Add($"Ext: {content.hasAudioEncodingFormat} - {content.bitRate}");

                            m_DownloadIndicies[downloadIndex] = EditorGUILayout.Popup(m_DownloadIndicies[downloadIndex], audioFiles.ToArray());

                            if (GUILayout.Button("Import"))
                            {
                                var contentToImport = member.content.availableAs[m_DownloadIndicies[downloadIndex]];
                                DownloadAndImportAudio(member.content.title,
                                    contentToImport.hasAudioEncodingFormat,
                                    contentToImport.locator);
                            }
                        }
                        GUILayout.EndHorizontal();
                        
                        ++downloadIndex;
                    }
                    GUILayout.EndVertical();
                }
            }
            
            EditorGUILayout.EndScrollView();
            EditorGUILayout.LabelField($"Number of Audio Clips found: {results.Sum(result => result.members.Count)}");

            GUILayout.BeginHorizontal(EditorStyles.helpBox);
            EditorGUILayout.LabelField($"Page: {m_CurrentPage}");

            bool disableBackButton = m_CurrentPage == 1;

            if (disableBackButton)
                GUI.enabled = false;

            if (GUILayout.Button("<"))
                QueryAudioCommonsApi(10, --m_CurrentPage);
            
            if(disableBackButton)
                GUI.enabled = true;

            if (GUILayout.Button(">"))
                QueryAudioCommonsApi(10, ++m_CurrentPage);
            
            GUILayout.EndHorizontal();
        }
    }

    private void QueryAudioCommonsApi(int limit, int page)
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

        m_AudioCommonsResponse = JsonUtility.FromJson<AudioCommonsResponse>(jsonResult);
        m_DownloadIndicies = Enumerable.Repeat(0, m_AudioCommonsResponse.results.Sum(result => result.members.Count)).ToList();
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
        EditorGUIUtility.PingObject(AssetDatabase.LoadMainAssetAtPath(assetPath));
    }
}