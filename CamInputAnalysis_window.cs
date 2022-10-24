
#if !(PLATFORM_LUMIN && !UNITY_EDITOR)
//controls the web cam and image processing
// pull values from here into face control
using DlibFaceLandmarkDetector;
using OpenCVForUnity.CoreModule;
using OpenCVForUnity.ImgprocModule;
using OpenCVForUnity.ObjdetectModule;
using OpenCVForUnity.UnityUtils.Helper;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Debug = UnityEngine.Debug;

[RequireComponent(typeof(WebCamTextureToMatHelper), typeof(ImageOptimizationHelper))]
public class CamInputAnalysis_window : MonoBehaviour
{
    //for image processing
    Mat grayMat; 
    CascadeClassifier cascade;
    WebCamTextureToMatHelper webCamTextureToMatHelper;
    ImageOptimizationHelper imageOptimizationHelper;
    FaceLandmarkDetector faceLandmarkDetector;
    Mat equalizeHistMat;
    MatOfRect faces;
    List<UnityEngine.Rect> detectionResult;
    List<UnityEngine.Rect> opencvDetectResult;

    List<Vector2> points=new List<Vector2>(); //list of facial landmark coordinates

    Texture2D texture;//to set camera output to object texture

    //variables for my landmark -> animation algorithm
    float base_w = 0f;
    float base_h = 0f;
    float unit_w = 0f;
    float furrow_x = 0f;
    float furrow_y = 0f;

    //for head turn side to side, not used atm
    //float l_turn = 0f;
    //float r_turn = 0f;

    public List<float> emotes = new List<float>(); //holds outputs from animation algorithm to then input to avatar

    //CV model file paths
    string haarcascade_frontalface_alt_xml_filepath;
    string dlibShapePredictorFileName = "sp_human_face_68.dat"; //add training with glasses?
    string dlibShapePredictorFilePath;

#if UNITY_WEBGL && !UNITY_EDITOR
    IEnumerator getFilePath_Coroutine;
#endif

    void Start()
    {
        equalizeHistMat = new Mat();
        faces = new MatOfRect();

        for (int i = 0; i < 12; i++)
        {
            emotes.Add(0f);
        }

        imageOptimizationHelper = gameObject.GetComponent<ImageOptimizationHelper>();
        webCamTextureToMatHelper = gameObject.GetComponent<WebCamTextureToMatHelper>();

        dlibShapePredictorFileName = "sp_human_face_68.dat";//DlibFaceLandmarkDetectorExample.dlibShapePredictorFileName;
#if UNITY_WEBGL && !UNITY_EDITOR
        getFilePath_Coroutine = GetFilePath ();
        StartCoroutine (getFilePath_Coroutine);
#else
        haarcascade_frontalface_alt_xml_filepath = OpenCVForUnity.UnityUtils.Utils.getFilePath("haarcascade_frontalface_alt.xml");
        dlibShapePredictorFilePath = DlibFaceLandmarkDetector.UnityUtils.Utils.getFilePath(dlibShapePredictorFileName);
        Run();
#endif
    }

#if UNITY_WEBGL && !UNITY_EDITOR
    private IEnumerator GetFilePath ()
    {
        var getFilePathAsync_0_Coroutine = OpenCVForUnity.UnityUtils.Utils.getFilePathAsync ("haarcascade_frontalface_alt.xml", (result) => {
            haarcascade_frontalface_alt_xml_filepath = result;
        });
        yield return getFilePathAsync_0_Coroutine;

        var getFilePathAsync_1_Coroutine = DlibFaceLandmarkDetector.UnityUtils.Utils.getFilePathAsync (dlibShapePredictorFileName, (result) => {
            dlibShapePredictorFilePath = result;
        });
        yield return getFilePathAsync_1_Coroutine;

        getFilePath_Coroutine = null;

        Run ();
    }
#endif

    private void Run()
    {
        cascade = new CascadeClassifier(haarcascade_frontalface_alt_xml_filepath); //initialize classifier
        faceLandmarkDetector = new FaceLandmarkDetector(dlibShapePredictorFilePath);

#if UNITY_ANDROID && !UNITY_EDITOR
        // Avoids the front camera low light issue that occurs in only some Android devices (e.g. Google Pixel, Pixel2).
        webCamTextureToMatHelper.avoidAndroidFrontCameraLowLightIssue = true;
#endif
        webCamTextureToMatHelper.Initialize();
    }

    void Update()
    {
        if (webCamTextureToMatHelper.IsPlaying() && webCamTextureToMatHelper.DidUpdateThisFrame())
        {
            Mat rgbaMat = webCamTextureToMatHelper.GetMat();
            if (!imageOptimizationHelper.IsCurrentFrameSkipped())//if we find that the camera skipped a frame for whatever reason, we'll skip our processing
            {
                Mat downScaleRgbaMat = imageOptimizationHelper.GetDownScaleMat(rgbaMat);
                float DOWNSCALE_RATIO = imageOptimizationHelper.downscaleRatio;

                OpenCVForUnityUtils.SetImage(faceLandmarkDetector, downScaleRgbaMat);// downscale the Mat

                Imgproc.cvtColor(downScaleRgbaMat, grayMat, Imgproc.COLOR_RGBA2GRAY); //convert to grayscale for processing
                Imgproc.equalizeHist(grayMat, equalizeHistMat);

                //control for minimum size detected
                //finds faces. While we only want one, this will allow for multiple if needed
                cascade.detectMultiScale(equalizeHistMat, faces, 1.1f, 3, 0 | Objdetect.CASCADE_SCALE_IMAGE, new Size(equalizeHistMat.cols() * 0.2, equalizeHistMat.cols() * 0.2), new Size());

                List<OpenCVForUnity.CoreModule.Rect> opencvDetectResult = faces.toList(); //list of each "face" we found
                detectionResult.Clear();//clear detection from previous frame
                foreach (var opencvRect in opencvDetectResult)
                {
                    detectionResult.Add(new UnityEngine.Rect((float)opencvRect.x, (float)opencvRect.y + (float)(opencvRect.height * 0.1f), (float)opencvRect.width, (float)opencvRect.height));
                }

                for (int i = 0; i < detectionResult.Count; ++i)
                {
                    var rect = detectionResult[i];
                    detectionResult[i] = new UnityEngine.Rect( rect.x * DOWNSCALE_RATIO, rect.y * DOWNSCALE_RATIO,
                        rect.width * DOWNSCALE_RATIO, rect.height * DOWNSCALE_RATIO);//downscale the face rectangle

                    //store information on user head size so animations are proportional
                    base_w = rect.width / 150f;
                    base_h = rect.height / 150f;
                    unit_w = rect.width / 5f;
                }

                OpenCVForUnityUtils.SetImage(faceLandmarkDetector, rgbaMat);// detect facial landmarks on our current image
               
                //this is the meat of my algorithm, taking (x,y) coordinates of facial landmarks and converting to a float(emotes[])
                //emotes[] then controls blendshapes for our avatar
                foreach (var rect in detectionResult)
                {
                    //grab landmark points for each face
                    points = faceLandmarkDetector.DetectLandmark(rect);

                    //eyebrow furrow
                    furrow_x = Mathf.Round((points[22].x - points[21].x) / base_w)-38;
                    furrow_y = Mathf.Round(((points[27].y - points[22].y) + (points[27].y - points[21].y)) / base_h)-49;
                    emotes[0]=(furrow_x+furrow_y)*-2;//take average of each eyebrow's furrow

                    emotes[1] = Mathf.Round((points[66].y - points[62].y) / base_h) * 3.5f; //Open_Mouth 

                    emotes[2] = (Mathf.Round((points[29].y - points[23].y) / base_h) - 70f) * 4f; //R_Eyebrow
                    emotes[3] = (Mathf.Round((points[29].y - points[20].y) / base_h) - 70f) * 4f; //L_Eyebrow

                    emotes[4] = (Mathf.Round((points[46].y - points[44].y) / base_h)); //R_Eye 
                    emotes[5] = (Mathf.Round((points[41].y - points[37].y) / base_h)); //L_Eye 

                    emotes[6] = ((Mathf.Round((points[54].x - points[51].x) / base_w)) - 45f) * 7f; //R_Mouth
                    emotes[7] = (Mathf.Round((points[51].x - points[48].x) / base_w) - 45f) * 7f; //L_Mouth

                    //How much of each side of the face can we see? (not using head turn atm)
                    //l_turn = Mathf.Round((points[27].x - points[0].x) / 10);
                    //r_turn = Mathf.Round((points[16].x - points[27].x) / 10);
                    //emotes[8] = (l_turn - r_turn) / (base_w);//turn

                    emotes[9] = (Mathf.Round((points[30].y - points[27].y) / base_h) - 55f) / 2; //nod
                    emotes[10] = (Mathf.Round((points[31].y - points[35].y) / base_h)); //tilt

                    //set eyebrows and smiles to be equal to 1/2 the side facing the camera for turn
                    //if (emotes[8] < -2f)
                    //{
                    //    emotes[2] = emotes[2] / 2f;
                    //    emotes[3] = emotes[2];

                    //    emotes[6] = emotes[6] / 2f;
                    //    emotes[7] = emotes[6];

                    //}
                    //else if (emotes[8] > 2f)
                    //{
                    //    emotes[3] = emotes[3] / 2f;
                    //    emotes[2] = emotes[3];

                    //    emotes[7] = emotes[7] / 2f;
                    //    emotes[6] = emotes[7];
                    //}

                    OpenCVForUnityUtils.DrawFaceLandmark(rgbaMat, points, new Scalar(0, 255, 0, 255), 2);//draw the landmark lines on mat
                    OpenCVForUnity.UnityUtils.Utils.fastMatToTexture2D(rgbaMat, texture); //set mat to our object's texture
                }

                //Smoothing in FaceControl helps but slows responsiveness.
                //I guess we would modify each base differently so that we didn't need to multiply at all
                //round(((point1-point2)-(val to zero))*(K/base))->integers 0-100

                //last thought: each person goes through to calibrate by showing the extremes of their facial movements
                //round(((point1-point2)-(val to zero))*(K/base))=100
                //round(((point1-point2)-(val to zero))*(K/base))=0
                //solve for K and val to zero

                //might need to convert these to absolute distances by calling a function(bases should still apply)
            }
        }
    }

    //private float average(float a, float b) //(unused)
    //{
    //    return (a + b) / 2f;
    //}

    //private float distance(Vector2 pointA,Vector2 pointB,float baseline,float dist,float coeff) //(unused)
    //{
    //    float a_2 = (pointA.x - pointB.x) * (pointA.x - pointB.x);
    //    float b_2 = (pointA.y - pointB.y) * (pointA.y - pointB.y);
    //    float c = (Mathf.Round(Mathf.Sqrt(a_2 + b_2)/baseline)-dist)*coeff;
    //    return c;
    //}

    //web cam calls below__________________________________________
    public void OnWebCamTextureToMatHelperInitialized()
    { Mat webCamTextureMat = webCamTextureToMatHelper.GetMat();
        Mat downscaleMat = imageOptimizationHelper.GetDownScaleMat(webCamTextureMat);

        texture = new Texture2D(webCamTextureMat.cols(), webCamTextureMat.rows(), TextureFormat.RGBA32, false);
        OpenCVForUnity.UnityUtils.Utils.fastMatToTexture2D(webCamTextureMat, texture);

        gameObject.GetComponent<Renderer>().material.mainTexture = texture;
        //Window.transform.localScale = new Vector3(webCamTextureMat.cols()/50, webCamTextureMat.rows()/50, 1);

        grayMat = new Mat(webCamTextureMat.rows(), webCamTextureMat.cols(), CvType.CV_8UC1);

        detectionResult = new List<UnityEngine.Rect>();
    }

    public void OnWebCamTextureToMatHelperDisposed()
    {
        //Debug.Log("OnWebCamTextureToMatHelperDisposed");

        if (grayMat != null)
        {
            grayMat.Dispose();
            grayMat = null;
        }
        if (texture != null)
        {
            Texture2D.Destroy(texture);
            texture = null;
        }
    }

    /// <param name="errorCode">Error code.</param>
    public void OnWebCamTextureToMatHelperErrorOccurred(WebCamTextureToMatHelper.ErrorCode errorCode)
    {
        //Debug.Log("OnWebCamTextureToMatHelperErrorOccurred " + errorCode);
    }

    void OnDestroy()
    {
        if (webCamTextureToMatHelper != null)
            webCamTextureToMatHelper.Dispose();

        if (imageOptimizationHelper != null)
            imageOptimizationHelper.Dispose();

        if (faceLandmarkDetector != null)
            faceLandmarkDetector.Dispose();

        if (cascade != null)
            cascade.Dispose();

#if UNITY_WEBGL && !UNITY_EDITOR
        if (getFilePath_Coroutine != null) {
            StopCoroutine (getFilePath_Coroutine);
            ((IDisposable)getFilePath_Coroutine).Dispose ();
        }
#endif
    }

}


#endif