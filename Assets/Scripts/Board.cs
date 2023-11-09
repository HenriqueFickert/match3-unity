using Newtonsoft.Json;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class Board : MonoBehaviour
{
    private UdpConnection connection;

    public MatchObject matchObjectPrefab;
    public int rows = 4;
    public int columns = 4;
    public float spacing = 1.1f;

    public ScrollRect scrollRect;
    public GameObject loggerPrefab;
    public Transform logContainer;

    public TMP_InputField inputLocalX;
    public TMP_InputField inputLocalY;
    public TMP_InputField inputDestinationX;
    public TMP_InputField inputDestinationY;

    private void Awake()
    {
        string sendIp = "127.0.0.1";
        int sendPort = 3000;
        int receivePort = Random.Range(11000, 11500);

        connection = new UdpConnection();
        connection.StartConnection(sendIp, sendPort, receivePort);
    }

    void Start()
    {
        connection.Send("ENTER");
    }

    void Update()
    {
        foreach (string message in connection.getMessages())
        {
            Debug.Log(message);
            CreateLog(message);

            string trimmedMessage = message.Trim();

            if (trimmedMessage.StartsWith("[[") && trimmedMessage.EndsWith("]]"))
            {
                try
                {
                    int[,] board = JsonConvert.DeserializeObject<int[,]>(trimmedMessage);
                    CreateBoard(board);
                }
                catch (JsonException ex)
                {
                    Debug.LogError("Fail to convert json: " + ex.Message);
                }
            }
        }
    }

    public void ButtonSend()
    {
        if (string.IsNullOrEmpty(inputLocalX.text) || string.IsNullOrEmpty(inputLocalY.text) || string.IsNullOrEmpty(inputDestinationX.text) || string.IsNullOrEmpty(inputDestinationY.text))
        {
            CreateLog("Invalid coordinate.");
            return;
        }

        // MOVE {"x":1,"y":0} {"x":0,"y":1}
        connection.Send(string.Format("MOVE {{\"x\":{0},\"y\":{1}}} {{\"x\":{2},\"y\":{3}}}", inputLocalX.text, inputLocalY.text, inputDestinationX.text, inputDestinationY.text));
    }

    public void CreateBoard(int[,] serverData)
    {
        int rows = serverData.GetLength(0);
        int columns = serverData.GetLength(1);

        float gridWidth = (columns - 1) * spacing;
        float gridHeight = (rows - 1) * spacing;
        Vector3 gridCenter = new (-gridWidth / 2, gridHeight / 2, 0);

        for (int i = 0; i < rows; i++)
        {
            for (int j = 0; j < columns; j++)
            {
                Vector3 position = new Vector3(j * spacing, -i * spacing, 0) + gridCenter;

                MatchObject newMatchObject = Instantiate(matchObjectPrefab, position, Quaternion.identity);
                newMatchObject.transform.SetParent(transform, false);

                newMatchObject.matchObjectData.value = serverData[i, j];
                newMatchObject.matchObjectData.row = i;
                newMatchObject.matchObjectData.column = j;

                newMatchObject.Initialize();
            }
        }
    }

    public void CreateLog(string message)
    {
        GameObject obj = Instantiate(loggerPrefab, logContainer.transform.position, Quaternion.identity);
        obj.transform.SetParent(logContainer.transform, false);
        obj.GetComponent<TextMeshProUGUI>().text = message;
        KeepMessageBottom();
    }

    private void KeepMessageBottom()
    {
        Canvas.ForceUpdateCanvases();
        scrollRect.content.GetComponent<VerticalLayoutGroup>().CalculateLayoutInputVertical();
        scrollRect.content.GetComponent<ContentSizeFitter>().SetLayoutVertical();
        scrollRect.verticalNormalizedPosition = 0;
    }

    void OnDestroy()
    {
        connection.Stop();
    }

}