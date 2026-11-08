using UnityEngine;
using TMPro;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using Unity.Cinemachine;
public class GreenWellMenu : MonoBehaviour {
 public TMP_FontAsset optionsFont, headingFont;
 public Camera menuCamera;
 public Transform storyTarget;
 public Vector3 startPosition;
 public Quaternion startRotation;

 public GameObject options, controls, quit;

 public CinemachineCamera chalkboardCamera, storyCamera;
 public CinemachineBrain cameraBrain;
void Start(){chalkboardCamera.Priority=20;storyCamera.Priority=10;Cursor.lockState=CursorLockMode.None;Cursor.visible=true;}
 
public void NewGame(){if(cameraBrain.IsBlending)return;storyCamera.Priority=20;chalkboardCamera.Priority=10;}

public void Back(){controls.SetActive(false);quit.SetActive(false);options.SetActive(true);}
 public void Controls(){options.SetActive(false);controls.SetActive(true);}
 public void ConfirmQuit(){options.SetActive(false);quit.SetActive(true);}
 public void Quit(){
 #if UNITY_EDITOR
 UnityEditor.EditorApplication.isPlaying=false;
 #else
 Application.Quit();
 #endif
 }
 public void Build(){
 var chalk=new Color(.94f,.91f,.82f,1);
 var root=new GameObject("Chalk Menu Canvas",typeof(RectTransform),typeof(Canvas),typeof(UnityEngine.UI.CanvasScaler),typeof(UnityEngine.UI.GraphicRaycaster));
 root.transform.SetParent(transform,false); root.transform.localPosition=new Vector3(0,0,.057f); root.transform.localRotation=Quaternion.Euler(0,180,0); root.transform.localScale=Vector3.one*.00048f;
 var canvas=root.GetComponent<Canvas>();canvas.renderMode=RenderMode.WorldSpace;canvas.worldCamera=menuCamera;
 root.GetComponent<RectTransform>().sizeDelta=new Vector2(1320,900);
 System.Func<Transform,string,float,float,float,float,RectTransform> rect=(p,n,x,y,w,h)=>{var o=new GameObject(n,typeof(RectTransform));o.transform.SetParent(p,false);var r=o.GetComponent<RectTransform>();r.sizeDelta=new Vector2(w,h);r.anchoredPosition=new Vector2(x,y);return r;};
 System.Func<Transform,string,float,float,float,float,int,TMP_FontAsset,TMP_Text> label=(p,s,x,y,w,h,size,font)=>{var r=rect(p,s,x,y,w,h);var t=r.gameObject.AddComponent<TextMeshProUGUI>();t.text=s;t.font=font;t.fontSize=size;t.color=chalk;t.alignment=TextAlignmentOptions.Center;t.raycastTarget=false;return t;};
 System.Action<Transform,float,float,float,float,float> line=(p,x,y,w,h,a)=>{var r=rect(p,"Chalk stroke",x,y,w,h);r.localRotation=Quaternion.Euler(0,0,a);var im=r.gameObject.AddComponent<UnityEngine.UI.Image>();im.color=chalk;im.raycastTarget=false;};
 System.Func<Transform,string,float,float,UnityEngine.Events.UnityAction,UnityEngine.UI.Button> button=(p,s,x,y,action)=>{var r=rect(p,s,x,y,500,80);var im=r.gameObject.AddComponent<UnityEngine.UI.Image>();im.color=new Color(1,1,1,.015f);var b=r.gameObject.AddComponent<UnityEngine.UI.Button>();b.targetGraphic=im;var col=b.colors;col.highlightedColor=new Color(1,1,1,.22f);col.selectedColor=col.highlightedColor;b.colors=col;b.onClick.AddListener(action);label(r,s,0,0,490,78,49,optionsFont);return b;};
 for(int i=0;i<30;i++)line(root.transform,-25+Mathf.Sin(i*3.1f)*1.3f,-365+i*25,2.5f,27,0);
 line(root.transform,0,405,1210,2,0);line(root.transform,0,-405,1210,2,0);
 for(int i=0;i<61;i++){label(root.transform,".",-600+i*20,382,15,20,21,optionsFont);label(root.transform,".",-600+i*20,-380,15,20,21,optionsFont);}
 string title="THE GREEN WELL";
 for(int i=0;i<title.Length;i++){float a=(150-i*120f/(title.Length-1))*Mathf.Deg2Rad;var t=label(root.transform,title[i].ToString(),-335+255*Mathf.Cos(a),20+255*Mathf.Sin(a),72,100,67,headingFont);t.rectTransform.localRotation=Quaternion.Euler(0,0,a*Mathf.Rad2Deg-90);}
 label(root.transform,"SALOON",-335,-215,520,85,66,headingFont);
 label(root.transform,"BROKEN BOUGH, OKLAHOMA",-335,-292,530,50,23,optionsFont);
 // Beer mug, drawn as chalk strokes with a foamy rim.
 line(root.transform,-410,15,5,165,-4);line(root.transform,-265,15,5,165,4);line(root.transform,-337,-67,145,5,0);
 line(root.transform,-230,40,65,5,0);line(root.transform,-202,0,5,80,0);line(root.transform,-230,-40,65,5,0);
 for(int i=0;i<3;i++)line(root.transform,-380+i*42,0,3,100,0);
 for(int i=0;i<7;i++){float a=i*Mathf.PI/3;label(root.transform,"o",-413+i*25,106+Mathf.Sin(a)*8,55,65,65,optionsFont);}
 options=rect(root.transform,"Menu Options",310,0,600,700).gameObject;
 label(options.transform,"HOLD MY BEER",0,285,530,65,53,headingFont);
 line(options.transform,0,232,430,2,0);
 button(options.transform,"New Game",0,155,NewGame);
 button(options.transform,"Sound",0,45,()=>{});
 button(options.transform,"Controls",0,-65,Controls);button(options.transform,"Quit",0,-175,ConfirmQuit);

 controls=rect(root.transform,"Controls Panel",310,0,600,700).gameObject;
 label(controls.transform,"CONTROLS",0,240,540,70,57,headingFont);
 label(controls.transform,"Move  -  WASD / Left Stick\n\nPause  -  Esc",0,60,540,240,34,optionsFont);
 button(controls.transform,"Back",0,-230,Back);controls.SetActive(false);
 quit=rect(root.transform,"Quit Confirmation",310,0,600,700).gameObject;
 label(quit.transform,"LEAVING SO SOON?",0,180,540,150,53,headingFont);
 button(quit.transform,"Quit Game",0,0,Quit);button(quit.transform,"Stay",0,-140,Back);quit.SetActive(false);
 }








public void StoryBack(){if(cameraBrain.IsBlending)return;chalkboardCamera.Priority=20;storyCamera.Priority=10;}
}
