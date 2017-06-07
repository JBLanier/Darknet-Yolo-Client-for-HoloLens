using UnityEngine;
using UnityEngine.VR.WSA.WebCam;
using System;
using System.Collections.Generic;
using System.Linq;
#if !UNITY_EDITOR && UNITY_METRO
using Windows.Networking.Sockets;
using Windows.Storage.Streams;
using Windows.Networking;
using Windows.Foundation;
#endif


/// <summary>
/// Handle taking pictures, send them to the backend and process the results
/// to display annotations in the real world.
/// </summary>
public class AnnotationHandling : MonoBehaviour
{
  
    [Tooltip("The connection port to connect to server with.")]
    public int m_connectionPort = 11000;

    Resolution m_cameraResolution;
    PhotoCapture m_photoCapture;

    public KeyboardMain m_keyboard;

    public float m_pictureInterval;
    public LayerMask m_raycastLayer;
    public GameObject m_annotationParent;
    public GameObject m_annotationTemplate;
    public GameObject m_annotationText;

#if !UNITY_EDITOR && UNITY_METRO
    private string m_serverIP;

    private Matrix4x4 m_projectionMatrix;

    private StreamSocket m_networkConnection;

    private DataWriter m_networkDataWriter;
    private DataReader m_networkDataReader;

    private bool m_inAnnotationMode = false;

    void Awake()
    {
        UnityThread.initUnityThread();
    }

    /// <summary>
    /// Use the start function to start the picture capturing process
    /// </summary>
    void Start()
    {
        Debug.Log("START()");

        //Get the lowest resolution
        m_cameraResolution = PhotoCapture.SupportedResolutions.OrderByDescending((res) => res.width * res.height).ElementAt(4);

        Debug.Log("Camera resolution " + m_cameraResolution.width + " x " + m_cameraResolution.height);
    }

    void beginAnnotationProcess()
    {
        PhotoCapture.CreateAsync(false, delegate (PhotoCapture captureObject)
        {
            //Assign capture object
            m_photoCapture = captureObject;

            //Configure camera
            CameraParameters cameraParameters = new CameraParameters();
            cameraParameters.hologramOpacity = 0.0f;
            cameraParameters.cameraResolutionWidth = m_cameraResolution.width;
            cameraParameters.cameraResolutionHeight = m_cameraResolution.height;
            cameraParameters.pixelFormat = CapturePixelFormat.JPEG;

            //Start the photo mode and start taking pictures
            m_photoCapture.StartPhotoModeAsync(cameraParameters, delegate (PhotoCapture.PhotoCaptureResult result)
            {
                //This block is the delegate for when photomode is started
                Debug.Log("Photo Mode started");
                ExecutePictureProcess();
            });


        });

        ReceiveDetectionsHeaderAsync();
    }

    public void ReceiveDetectionsHeaderAsync()
    {
        DataReaderLoadOperation drlo = m_networkDataReader.LoadAsync(136);
        drlo.Completed = new AsyncOperationCompletedHandler<uint>(DetectionHeaderReceivedHandler);
    }

    /// <summary>
    /// Clean up on destroy
    /// </summary>
    void OnDestroy()
    {
        m_inAnnotationMode = false;
        StopAndDestroyPhotoCapture();
    }

    void StopAndDestroyPhotoCapture()
    {
        UnityThread.executeInUpdate(() =>
        {
            if (m_photoCapture != null)
            {
                m_photoCapture.StopPhotoModeAsync(
                  delegate (PhotoCapture.PhotoCaptureResult res)
                  {
                      m_photoCapture.Dispose();
                      m_photoCapture = null;
                      Debug.Log("Photo Mode stopped");
                  }
                );
            }
        });
    }

    void OnPhotoTaken(PhotoCapture.PhotoCaptureResult result, PhotoCaptureFrame photoCaptureFrame)
    {
        Debug.Log("In photocapture delegate");

        if (m_networkConnection != null && m_inAnnotationMode)
        {

            List<byte> buffer = new List<byte>();
            Matrix4x4 cameraToWorldMatrix;
            Matrix4x4 cameraProjectionMatrix;

            photoCaptureFrame.CopyRawImageDataIntoBuffer(buffer);



            //Check if we can receive the position where the photo was taken
            if (!photoCaptureFrame.TryGetProjectionMatrix(out cameraProjectionMatrix))
            {
                Debug.Log("Couldn't get camera projection matrix");

                //Try again
                m_photoCapture.TakePhotoAsync(OnPhotoTaken);

            } else if (!photoCaptureFrame.TryGetCameraToWorldMatrix(out cameraToWorldMatrix))
            {
                Debug.Log("Couldn't get camera to world matrix");

                //Try again
                m_photoCapture.TakePhotoAsync(OnPhotoTaken);
                
            } else {
                m_projectionMatrix = cameraProjectionMatrix;

                //Send The frame to server
                SendFrameDataOverNetwork(buffer.ToArray(), cameraToWorldMatrix, cameraProjectionMatrix);

                //Take another photo
                m_photoCapture.TakePhotoAsync(OnPhotoTaken);
            }
        } else
        {
            Debug.Log("Photo taken but we're either not connected or not in photomode so not sending it.");
        }

    }

    /// <summary>
    /// Take a photo anbd start the backend handling
    /// </summary>
    void ExecutePictureProcess()
    {
        if (m_photoCapture != null)
        {
            m_photoCapture.TakePhotoAsync(OnPhotoTaken);
        }
    }

    Vector3 CalcTopLeftVector(int x1, int y1, int x2, int y2)
    {
        Vector3 vector = new Vector3(x1, y1, 0);
        return ScaleVector(vector);
    }

    Vector3 CalcTopRightVector(int x1, int y1, int x2, int y2)
    {
        Vector3 vector = new Vector3(x2, y1, 0);
        return ScaleVector(vector);
    }

    Vector3 CalcBottomRightVector(int x1, int y1, int x2, int y2)
    {
        Vector3 vector = new Vector3(x2, y2, 0);
        return ScaleVector(vector);
    }

    Vector3 CalcBottomLeftVector(int x1, int y1, int x2, int y2)
    {
        Vector3 vector = new Vector3(x1, y2, 0);
        return ScaleVector(vector);
    }

    Vector3 ScaleVector(Vector3 vector)
    {
        float scaleX = (float)Screen.width / (float)m_cameraResolution.width;
        float scaleY = (float)Screen.height / (float)m_cameraResolution.height;

        return vector * Math.Max(scaleX, scaleY);
    }

    private bool Sending = false;

#endif


    public void ConnectToServer(string hostname)
    {
#if !UNITY_EDITOR && UNITY_METRO
        m_inAnnotationMode = true;
        m_serverIP = hostname;

        // Setup a connection to the server.
        try
        {
            HostName networkHost = new HostName(hostname.Trim());
            if (networkHost != null)
            {
                Debug.Log("Connecting to " + networkHost.ToString());
                m_networkConnection = new StreamSocket();

                // Connections are asynchronous.
                IAsyncAction outstandingAction = m_networkConnection.ConnectAsync(networkHost, m_connectionPort.ToString());
                AsyncActionCompletedHandler aach = new AsyncActionCompletedHandler(NetworkConnectedHandler);
                outstandingAction.Completed = aach;
            } else
            {
                Debug.Log("ERROR: Hostname \"" + hostname + "\" didn't resolve.");
                ResetEverything();
            }
        } catch (Exception e)
        {
            Debug.Log("Error in setting up connection:\n" + e);
            ResetEverything();
        }
        
#endif
    }
   
    public void DisconnectFromServer()
    {
#if !UNITY_EDITOR && UNITY_METRO
        if (m_networkConnection != null)
        {
            m_networkConnection.Dispose();
            m_networkConnection = null;
        }
        if (m_networkDataReader != null)
        {
            m_networkDataReader.Dispose();
            m_networkDataReader = null;
        }
        if (m_networkDataWriter != null)
        {
            m_networkDataWriter.Dispose();
            m_networkDataWriter = null;
        }
#endif
    }

#if !UNITY_EDITOR && UNITY_METRO

    public void NetworkConnectedHandler(IAsyncAction asyncInfo, AsyncStatus status)
    {
        // Status completed is successful.
        if (status == AsyncStatus.Completed)
        {
            m_networkDataWriter = new DataWriter(m_networkConnection.OutputStream);
            m_networkDataReader = new DataReader(m_networkConnection.InputStream);
            Debug.Log("Connected to Host");

            Action beginAnnotations = beginAnnotationProcess;
            UnityThread.executeInUpdate(beginAnnotations);

            m_keyboard.Deactivate();
        }
        else
        {
            Debug.Log("Failed to establish connection. Error Code: " + asyncInfo.ErrorCode);
            ResetEverything();
        }
    }

    public void DetectionHeaderReceivedHandler(IAsyncOperation<uint> operation, AsyncStatus status)
    {
        if (status == AsyncStatus.Error)
        {
            Debug.Log("THERE WAS A READ ERROR recieving the detection header");
            ResetEverything();
        }
        else
        {
            try
            {
                Matrix4x4 ctw = Matrix4x4.zero;
                for (int i = 0; i < 4; i++)
                {
                    for (int j = 0; j < 4; j++)
                    {
                        ctw[i, j] = m_networkDataReader.ReadSingle();
                    }

                }

                Matrix4x4 projection = Matrix4x4.zero;
                for (int i = 0; i < 4; i++)
                {
                    for (int j = 0; j < 4; j++)
                    {
                        projection[i, j] = m_networkDataReader.ReadSingle();
                    }

                }

                int num = m_networkDataReader.ReadInt32();
                uint total_size = m_networkDataReader.ReadUInt32();
                Debug.Log("Header Received, num: " + num + " total size: " + total_size + "\nCTW:\n" + ctw.ToString() + "\nProjection:\n" + projection.ToString());

                if (num > 0 && total_size > 0)
                {
                    DataReaderLoadOperation drlo = m_networkDataReader.LoadAsync(total_size);
                    drlo.Completed = new AsyncOperationCompletedHandler<uint>(
                        (op, stat) => DetectionsBodyReceivedHandler(op, stat, num, total_size, ctw, projection)
                    );
                }
                else
                {
                    ReceiveDetectionsHeaderAsync();
                }
            } catch (Exception e)
            {
                Debug.Log("There was an error reading the recieved detection header:\n" + e);
                ResetEverything();
            }
        }
    }

#endif

    public void ResetEverything()
    {
#if !UNITY_EDITOR && UNITY_METRO
        if (m_inAnnotationMode)
        {
            m_inAnnotationMode = false;
            StopAndDestroyPhotoCapture();
            DisconnectFromServer();
            UnityThread.executeInUpdate(DestroyAnnotations);
            m_keyboard.ResetKeyboard();
        }
#endif
    }
#if !UNITY_EDITOR && UNITY_METRO

    void DestroyAnnotations()
    {
        //Remove all annotations
        foreach (Transform child in m_annotationParent.transform)
        {
            Destroy(child.gameObject);
        }
    }

    public static Vector3 UnProjectVector(Matrix4x4 proj, Vector3 to)
    {
        Vector3 from = new Vector3(0, 0, 0);
        var axsX = proj.GetRow(0);
        var axsY = proj.GetRow(1);
        var axsZ = proj.GetRow(2);
        from.z = to.z / axsZ.z;
        from.y = (to.y - (from.z * axsY.z)) / axsY.y;
        from.x = (to.x - (from.z * axsX.z)) / axsX.x;
        return from;
    }

    public Vector3 pixelToWorldSpace(int x, int y, Matrix4x4 cameraToWorld)
    {
        Vector2 ImagePosZeroToOne = new Vector2(x * 1.0f / m_cameraResolution.width, y * 1.0f / m_cameraResolution.height);
        Vector2 ImagePosProjected = (ImagePosZeroToOne * 2.0f) - new Vector2(1, 1); // -1 to 1 space
        Vector3 CameraSpacePos = UnProjectVector(m_projectionMatrix, new Vector3(ImagePosProjected.x, ImagePosProjected.y, 1));
        Vector3 WorldSpaceRayPoint1 = cameraToWorld.MultiplyPoint(Vector3.zero); // camera location in world space
        Vector3 WorldSpaceRayPoint2 = cameraToWorld.MultiplyPoint(CameraSpacePos); // ray point in world space

        RaycastHit hit;
        if (Physics.Raycast(WorldSpaceRayPoint1, WorldSpaceRayPoint2 - WorldSpaceRayPoint1, out hit, 15.0f, m_raycastLayer))
        {
            return hit.point;
        } else
        {
            Debug.Log("Raycast FAILED");
            return Vector3.zero;
        }
    }

    public Ray pixelToWorldSpaceRay(int x, int y, Matrix4x4 cameraToWorld)
    {
        Vector2 ImagePosZeroToOne = new Vector2(x * 1.0f / m_cameraResolution.width, y * 1.0f / m_cameraResolution.height);
        Vector2 ImagePosProjected = (ImagePosZeroToOne * 2.0f) - new Vector2(1, 1); // -1 to 1 space
        Vector3 CameraSpacePos = UnProjectVector(m_projectionMatrix, new Vector3(ImagePosProjected.x, ImagePosProjected.y, 1));
        //CameraSpacePos.y -= 0.07f;
        Vector3 WorldSpaceRayPoint1 = cameraToWorld.MultiplyPoint(Vector3.zero); // camera location in world space
        Vector3 WorldSpaceRayPoint2 = cameraToWorld.MultiplyPoint(CameraSpacePos); // ray point in world space

        return new Ray(WorldSpaceRayPoint1, WorldSpaceRayPoint2 - WorldSpaceRayPoint1);
    }

    public void DetectionsBodyReceivedHandler(IAsyncOperation<uint> operation, AsyncStatus status,int num, uint total_size, Matrix4x4 ctw, Matrix4x4 projection)
    {
        if (status == AsyncStatus.Error)
        {
            Debug.Log("THERE WAS A READ ERROR receiving the detections body");
            ResetEverything();
        }
        else
        {
            Debug.Log("BODY RECEIVED, length: " + m_networkDataReader.UnconsumedBufferLength);
            UnityThread.executeInUpdate(() =>
            {
                try
                {

                    Debug.Log("Refreshing annotations in main thread");
                    //Remove old annotations
                    DestroyAnnotations();

                    for (int i = 0; i < num; i++)
                    {
                        int left = m_networkDataReader.ReadInt32();
                        int top = m_cameraResolution.height - m_networkDataReader.ReadInt32();
                        int right = m_networkDataReader.ReadInt32();
                        int bottom = m_cameraResolution.height - m_networkDataReader.ReadInt32();
                        int red = m_networkDataReader.ReadInt32();
                        int green = m_networkDataReader.ReadInt32();
                        int blue = m_networkDataReader.ReadInt32();
                        uint label_size = m_networkDataReader.ReadUInt32();
                        Debug.Log("label size: " + label_size);
                        String label = m_networkDataReader.ReadString(label_size);

                        Debug.Log("BOX:\nleft: " + left + "\ntop: " + top + "\nright: " + right + "\nbottom: " + bottom + "\nRed: " + red
                            + "\nGreen: " + green + "\nBlue: " + blue + "\nlabel: " + label);



                        Ray centerRay = pixelToWorldSpaceRay((left + right) / 2, (top + bottom) / 2, ctw);

                        RaycastHit centerHit;
                        if (Physics.Raycast(centerRay, out centerHit, Mathf.Infinity, m_raycastLayer))
                        {
                            Ray topLeftRay = pixelToWorldSpaceRay(left, top, ctw);
                            Ray topRightRay = pixelToWorldSpaceRay(right, top, ctw);
                            Ray bottomLeftRay = pixelToWorldSpaceRay(left, bottom, ctw);

                            float distance = centerHit.distance;
                            float goScaleX = Vector3.Distance(topLeftRay.GetPoint(distance), topRightRay.GetPoint(distance));
                            float goScaleY = Vector3.Distance(topLeftRay.GetPoint(distance), bottomLeftRay.GetPoint(distance));

                            GameObject go = Instantiate(m_annotationTemplate) as GameObject;
                            go.transform.SetParent(m_annotationParent.transform);
                            go.transform.rotation = Quaternion.LookRotation(Camera.main.transform.forward, Vector3.up);
                            go.transform.position = centerHit.point;

                            go.transform.localScale = new Vector3(goScaleX / 1.8f, goScaleY / 1.8f, 0.1f);
                            go.GetComponentInChildren<Renderer>().material.color = new Color(red / 255.0f, green / 255.0f, blue / 255.0f);

                            GameObject text = Instantiate(m_annotationText) as GameObject;
                            text.transform.SetParent(go.transform);
                            text.transform.position = go.transform.position;
                            float textScaleX = goScaleX / 14f;
                            float textScaleY = goScaleY / 14f;
                            if (textScaleX < 0.05f) { textScaleX = 0.05f; }
                            if (textScaleY < 0.05f) { textScaleY = 0.05f; }
                            text.transform.localScale = new Vector3(textScaleX, textScaleY, 0.1f);
                            text.transform.rotation = Quaternion.LookRotation(Camera.main.transform.forward, Vector3.up);
                            text.GetComponent<TextMesh>().text = label;
                            text.GetComponent<TextMesh>().fontSize = 40;

                            /*
                            Debug.DrawLine(centerRay.origin, centerHit.point, Color.red, 2, false);

                            GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                            sphere.transform.position = centerHit.point;
                            sphere.transform.SetParent(m_annotationParent.transform);
                            sphere.transform.localScale = new Vector3(0.05f, 0.05f, 0.05f);
                            sphere.GetComponent<Renderer>().material.color = Color.red;


                            GameObject sphere1 = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                            sphere1.transform.position = centerRay.origin;
                            sphere1.transform.SetParent(m_annotationParent.transform);
                            sphere1.transform.localScale = new Vector3(0.05f, 0.05f, 0.05f);
                            sphere1.GetComponent<Renderer>().material.color = Color.blue;

                            GameObject sphere2 = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                            sphere2.transform.position = centerRay.direction;
                            sphere2.transform.SetParent(m_annotationParent.transform);
                            sphere2.transform.localScale = new Vector3(0.05f, 0.05f, 0.05f);
                            sphere2.GetComponent<Renderer>().material.color = Color.cyan;
                            */

                        }
                        else
                        {
                            Debug.Log("Raycast FAILED");
                        }
                    }

                    ReceiveDetectionsHeaderAsync();

                } catch (Exception e)
                {
                    Debug.Log("There was an error reading the recieved detection body:\n" + e);
                    ResetEverything();
                }
            });

            
        }
    }
        
    private void SendFrameDataOverNetwork(byte[] dataBufferToSend, Matrix4x4 cameraToWorldMatrix, Matrix4x4 cameraProjectionMatrix)
    {
        try
        {
            //Debug.Log("sending");
            if (Sending)
            {
                // This shouldn't happen, but just in case.
                Debug.Log("SEND ERROR: one at a time please");
                return;
            }

            // Track that we are sending a data buffer.
            Sending = true;

            // Write how much data we are sending.
            //Debug.Log("Length of frame to send: " + dataBufferToSend.Length);
            //Debug.Log("Sending frame size");
            m_networkDataWriter.WriteInt32(dataBufferToSend.Length);

            //mNetworkDataWriter.WriteString(dataBufferToSend.Length.ToString());

            // Then write the data.
            //Debug.Log("Sending frame data");
            m_networkDataWriter.WriteBytes(dataBufferToSend);

            Debug.Log("Sending CTW Matrix: " + cameraToWorldMatrix.ToString());
            for (int i = 0; i < 4; i++)
            {
                for (int j = 0; j < 4; j++)
                {
                    m_networkDataWriter.WriteSingle(cameraToWorldMatrix[i, j]);
                }

            }

            Debug.Log("Sending Projection Matrix: " + cameraProjectionMatrix.ToString());
            for (int i = 0; i < 4; i++)
            {
                for (int j = 0; j < 4; j++)
                {
                    m_networkDataWriter.WriteSingle(cameraProjectionMatrix[i, j]);
                }

            }

            // Again, this is an async operation, so we'll set a callback.
            DataWriterStoreOperation dswo = m_networkDataWriter.StoreAsync();
            dswo.Completed = new AsyncOperationCompletedHandler<uint>(DataSentHandler);

        } catch (ObjectDisposedException e)
        {
            Debug.Log("Tried to send frame data but connection was disposed:\n" + e);
            //Do nothing
        }
    
    }

    public void DataSentHandler(IAsyncOperation<uint> operation, AsyncStatus status)
    {
        // If we failed, requeue the data and set the deferral time.
        Sending = false;
        if (status == AsyncStatus.Error)
        {
            Debug.Log("THERE WAS A SEND ERROR sending frame data");
            ResetEverything();
        }
 
    }


#endif
}
