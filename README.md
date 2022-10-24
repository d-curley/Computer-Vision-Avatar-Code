# Cam_Avatar_in_Unity
Project to control a 3D avatar using computer vision in unity. See it in action here: https://www.youtube.com/watch?v=LuVUgmsSVyU&feature=emb_title

I have taken the OpenCV for Unity Facial Landmark Tracking asset, specifically the (x,y) coordinates of each of the 68 facial landmarks in each frame.
-I developed an algorithm to convert those points into blendshape values 0-100: 
Mouth open and closed, eye blink, left and right eyebrow up, down, and furrow, left and right mouth corners out and in
-In addition to blendshapes, I use the landmark tracking to approximate head tilt and nod.
-I took an avatar asset and designed my own blendshapes in Blender and imported into unity
-Using the outputs from my algorithm, I can control the avatar's facial expression and head movements, all from the webcam input. No depth sensing/LiDAR required!

Objective: A client was looking for a system that would allow a user on any platform to control a 3D avatar with their facial expressions. 
While Unity is not typically used for computer vision applications like this, the cross platform development and 3D model control encouraged me to make it work.

Description: Starting with an open source 3D file to serve as my avatar, I design a number of facial expression elements (eyebrow raised vs lower, mouth open vs closed, smile vs frown) using sculpting and vertex manipulation. Bringing my avatar into Unity, I could then control each of those expressions with code. These expressions are controlled by tracking the user’s facial landmarks using a computer vision Asset (openCV). I developed an algorithm to convert those landmark locations into values to accurately control the expressions on the Avatar.

For my contributions see Assets/My Active Scripts/
