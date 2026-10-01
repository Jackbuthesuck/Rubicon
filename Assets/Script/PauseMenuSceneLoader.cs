using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class PauseMenuSceneLoader : MonoBehaviour
{
    [SerializeField] private string sceneName = "PauseMenu";

    private void Awake()
    {
        if (!Application.isPlaying || string.IsNullOrWhiteSpace(sceneName))
        {
            return;
        }

        if (SceneManager.GetSceneByName(sceneName).isLoaded)
        {
            return;
        }

        SceneManager.LoadScene(sceneName, LoadSceneMode.Additive);
    }
}