using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using System;
using System.Collections;
using System.Collections.Generic;
using OpenCVForUnity.CoreModule;
using OpenCVForUnity.ObjdetectModule;
using OpenCVForUnity.ImgprocModule;
using OpenCVForUnity.UnityUtils;
using OpenCVForUnity.UnityUtils.Helper;
using System.Diagnostics;
using Debug = UnityEngine.Debug;

[RequireComponent(typeof(CamInputAnalysis_window))]
public class FaceControl_windowed : MonoBehaviour
{
    public SkinnedMeshRenderer skinnedMeshRenderer; //holds avatar blendshapes
    CamInputAnalysis_window myFaceTrack; //pulls emotes[] from CamInputAnalysis to control blendshapes

    public Animation blink;//blink is the only non-blendshape animation, instead check if eyes are closed or not and initializing the blink animation I deisgned

    public Transform Head; //this controls head tilt and nod 
    float tilt = 0f; 
    float nod = 0f;

    //controls to enable...
    public Toggle Symmetry;//...symmetrical facial expressions (gains smoother animation at the cost of assymetrical expressions)
    public Toggle HeadMovement;//...head tilt and nod, keeping the head still
    public Toggle Brow_Furrow;//...eyebrows to furrow

    //these indeces keep track of blendshapes
    int O_Mouth_index = 0;
    int R_Brow_index = 2;
    int L_Brow_index = 3;
    int R_Mouth_index = 4;
    int L_Mouth_index = 5;
    int Furrow_index = 6;

    List<float> emotions = new List<float>();//hold current emotes from CamInputAnalysis script
    public List<float> last = new List<float>();//the previous frame's emotes. These are used to weight the current animation to prevent rapid unnatural movement

    private static float R_Mouth;
    private static float L_Mouth;
    private static float O_Mouth;
    private static float R_Brow;
    private static float L_Brow;
    float furrow;


    //this was an idea I had to only trigger mouth movement if the microphone pick up audio. It needs refinement
    //AudioClip microphoneInput;
    //public float sensitivity;
    //int dec = 128;

    Quaternion target; //for head rotation
    void Awake()
    {
        //for audio input, if needed later
        //if (Microphone.devices.Length > 0){ microphoneInput = Microphone.Start(Microphone.devices[0], true, 999, 44100);}
        blink.Play("blink");

        myFaceTrack = GetComponent<CamInputAnalysis_window>(); 

        for(int i = 0; i < 12; i++) {//initializes emotions[], which we need since we usually don't grab facial landmarks on the first frame
            emotions.Add(0f);
            last.Add(0f); 
        }

        Symmetry.isOn = false;
    }

    void Update()
    {
        emotions = myFaceTrack.emotes;

        if (HeadMovement.isOn)
        {
            //control head non
            nod = Mathf.Clamp((emotions[9] + last[9] * 2) / 3, -6, 6);
            if (Mathf.Abs(nod) < 3) { nod = 0f; }

            //control head tilt
            tilt = Mathf.Clamp((emotions[10] + last[10] * 2) / 3, -10, 10);//bring some of this to camanalysis script
            if (Mathf.Abs(tilt) < 4) { tilt = 0f; }

            target = Quaternion.Euler(nod, tilt+180, 10); 
        }
        else
        {
            target = Quaternion.Euler(0, 180, 0); //keep head straight
        }
        Head.rotation = Quaternion.Slerp(Head.rotation, target, Time.deltaTime * 2.5f);// Dampen rotation

        if (Mathf.Abs(emotions[9]) < 6 && Mathf.Abs(emotions[10]) < 10) //only run if the user is showing enough of their face to the camera
        {
            if (emotions[4] < 5f) //if the eyelids are close together
            {
                blink.Play("blink");
            }

            //clamps are to keep blendshapes in a natural range
            //calculations with "_Last" variables average the measured emoation of this frame with the previous one
            //that has the effect of smoothing out the animations
            R_Brow = Mathf.Clamp(emotions[2], 0, 80);
            R_Brow = (R_Brow + last[2] * 4) / 5;

            L_Brow = Mathf.Clamp(emotions[3], 0, 80);
            L_Brow = (L_Brow + last[3] * 4) / 5;

            R_Mouth = Mathf.Clamp(emotions[6], 0, 100);
            R_Mouth = (R_Mouth + last[6]*3) / 4;

            L_Mouth = Mathf.Clamp(emotions[7], 0, 100);
            L_Mouth = (L_Mouth + last[7]*3) / 4;

            if (Symmetry.isOn) //set left and right mouth corners and eyebrows equal
            {
                L_Mouth = R_Mouth;
                L_Brow = R_Brow;
            }

            if (Brow_Furrow.isOn)
            {
                if (R_Brow < 1)
                {
                    furrow = Mathf.Clamp(emotions[0], 0, 100);
                    if (furrow < 24)
                    {
                        furrow = 12;
                    }
                    if (furrow > 60)
                    {
                        furrow = Mathf.Round(furrow / 5) * 5;
                    }
                    furrow = (furrow + last[0] * 5) / 6;
                }
                else
                {
                    furrow = 12;
                }
            }
            else
            {
                furrow = 0;
            }

            skinnedMeshRenderer.SetBlendShapeWeight(R_Brow_index, R_Brow);
            skinnedMeshRenderer.SetBlendShapeWeight(L_Brow_index, L_Brow);
            skinnedMeshRenderer.SetBlendShapeWeight(R_Mouth_index, R_Mouth);
            skinnedMeshRenderer.SetBlendShapeWeight(L_Mouth_index, L_Mouth);
            skinnedMeshRenderer.SetBlendShapeWeight(Furrow_index, furrow);
        }


        //float level = audioOut();

        //if (level > audiothresh)
        //{
        //    O_Mouth = Mathf.Clamp(emotions[1], 0, 100);
        //}
        //else
        //{
        //    O_Mouth = 0f;
        //}

        O_Mouth = Mathf.Clamp(emotions[1], 0, 101);
        O_Mouth = (O_Mouth + last[1]*2) / 3;
        if (O_Mouth < 7)
        {
            O_Mouth = 4;
        }
        if (O_Mouth > 45)
        {
            O_Mouth = Mathf.Round(O_Mouth / 5) * 5; //smooths out mout movements when mouth is open wide
        }
        skinnedMeshRenderer.SetBlendShapeWeight(O_Mouth_index, O_Mouth);
        last = emotions;
    }

    //this was an idea I had to only trigger mouth movement if the microphone pick up audio. It needs refinement
    //private float audioOut()
    //{
    //    float[] waveData = new float[dec];
    //    int micPosition = Microphone.GetPosition(null) - (dec + 1); // null means the first microphone
    //    microphoneInput.GetData(waveData, micPosition);

    //    // Getting a peak on the last 128 samples
    //    float levelMax = 0;
    //    for (int i = 0; i < dec; i++)
    //    {
    //        float wavePeak = waveData[i] * waveData[i];
    //        if (levelMax < wavePeak)
    //        {
    //            levelMax = wavePeak;
    //        }
    //    }
    //    return Mathf.Sqrt(Mathf.Sqrt(levelMax));
    //}
}
