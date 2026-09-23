using UnityEngine;

using UnityEditor;

using System.Linq;

using System.Collections.Generic;

using System.IO;

using System.Text.RegularExpressions;

using System.Text;
using System;

public class AutoBuildTemplate
{
    public static string UIClass =

 @"
 /********************************************************************
               _ooOoo_
              o8888888o
              88"". ""88
              (| -_- |)
              O\  =  /O
           ____/`---'\____
         .'  \\|     |//  `.
        /  \\|||  :  |||//  \
       /  _||||| -:- |||||-  \
       |   | \\\  -  /// |   |
       | \_|  ''\---/''  |   |
       \  .-\__  `-`  ___/-. /
     ___`. .'  /--.--\  `. . __
  ."" '<  `.___\_<|>_/___.'  >'"".
 | | :  `- \`.;`\ _ /`;.`/ - ` : | |
 \  \ `-.   \_ __\ /__ _/   .-` /  /
======`-.____`-.___\_____/___.-`____.-'======
               `=---='
^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^
佛祖保佑 永无BUG       永不修改

*********************************************************************/

using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class #类名# : #继承#
{
    //autoStart
    #成员#
    private void Awake()
	{
		#查找#
	}
    //autoEnd
    //TODO
    private void Start()
    {
       
    }
    //
    private void OnEnable()
    {
     
    }
}
";

}

public class AutoBuild : EditorWindow

{

    [MenuItem("MrTool/创建或刷新界面")]
    public static void BuildUIScript()
    {
        GetWindow<AutoBuild>("Auto Build UI Script");
    }
    /// <summary>
    /// 选择选项的下标 
    /// </summary>
    private int selectedOption = 0;
    /// <summary>
    /// 是否显示自定义选项
    /// </summary>
    bool showTextField = false;
    /// <summary>
    /// 需要继承类的名称
    /// </summary>
    private string inputString = "MonoBehaviour";
    /// <summary>
    /// 自定义选项
    /// </summary>
    private string[] options = { "MonoBehaviour", "HomelandUIBase", "自定义" };
    private void OnGUI()
    {
        GUILayout.Label("选择需要继承的类:", EditorStyles.boldLabel);
        for (int i = 0; i < options.Length; i++)
        {
            EditorGUILayout.BeginHorizontal();
            {
                bool isSelected = (i == selectedOption);
                bool newIsSelected = EditorGUILayout.ToggleLeft(options[i], isSelected);
                if (newIsSelected && !isSelected)
                {
                    selectedOption = i;

                    // 执行特定的操作
                    OnOptionSelected(i);
                }
            }
            EditorGUILayout.EndHorizontal();
        }

        if (showTextField)
        {
            inputString = EditorGUILayout.TextField("输入要继承类的名称:", inputString);
        }

        if (GUILayout.Button("生成"))
        {
            BuildDefaultUIScript(inputString);
        }


    }

    private void OnOptionSelected(int optionIndex)
    {
        showTextField = optionIndex == options.Length - 1;
        if (optionIndex != options.Length - 1)
        {
            inputString = options[optionIndex];
        }
    }

    public static void BuildDefaultUIScript(string inherit = "MonoBehaviour")
    {
        var dicUIType = new Dictionary<string, string>();

        dicUIType.Add("Img", "Image");

        dicUIType.Add("Btn", "Button");

        dicUIType.Add("Txt", "Text");

        dicUIType.Add("Tran", "Transform");

        dicUIType.Add("Raw", "RawImage");

        dicUIType.Add("Obj", "GameObject");

        dicUIType.Add("TxtM", "TextMeshProUGUI");

        dicUIType.Add("Sli", "Slider");

        dicUIType.Add("Drop", "Dropdown");

        dicUIType.Add("Tog", "Toggle");

        dicUIType.Add("Togg", "ToggleGroup");
        dicUIType.Add("CanvasG","CanvasGroup");


        GameObject[] selectobjs = Selection.gameObjects;

        foreach (GameObject go in selectobjs)
        {
            //选择的物体
            //GameObject selectobj = go.transform.root.gameObject;
            GameObject selectobj = go.gameObject;

            //物体的子物体

            Transform[] _transforms = selectobj.GetComponentsInChildren<Transform>(true);

            List<Transform> childList = new List<Transform>(_transforms);

            //UI需要查询的物体

            var mainNode = from trans in childList where trans.name.Contains('_') && dicUIType.Keys.Contains(trans.name.Split('_')[0]) select trans;

            var nodePathList = new Dictionary<string, string>();
            //循环得到物体路径
            foreach (Transform node in mainNode)

            {

                Transform tempNode = node;
                string nodePath = "/" + tempNode.name;
                //while (tempNode != tempNode.root)
                while (tempNode != selectobj.transform)
                {
                    tempNode = tempNode.parent;

                    int index = nodePath.IndexOf('/');

                    nodePath = nodePath.Insert(index, "/" + tempNode.name);

                }
                //int length = (selectobj.transform.name.Length / 2) + 1 ;
                nodePath = nodePath.Replace("/" + selectobj.transform.name, "");
                nodePath = nodePath.Substring(1);
                //Debug.LogError(nodePath.Substring(1));
                //Debug.LogError(nodePath);
                nodePathList.Add(node.name, nodePath);

            }
            //成员变量字符串

            string memberstring = "";

            //查询代码字符串

            string loadedcontant = "";

            foreach (Transform itemtran in mainNode)
            {
                string typeStr = dicUIType[itemtran.name.Split('_')[0]];

                memberstring += "private " + typeStr + " " + itemtran.name + " = null;\r\n\t";
                if (typeStr == "Transform")
                {
                    loadedcontant += itemtran.name + " = " + "transform.Find(\"" + nodePathList[itemtran.name] + "\");\r\n\t\t";
                }
                else if (typeStr == "GameObject")
                {
                    loadedcontant += itemtran.name + " = " + "transform.Find(\"" + nodePathList[itemtran.name] + "\").gameObject;\r\n\t\t";
                }
                else
                {
                    loadedcontant += itemtran.name + " = " + "transform.Find(\"" + nodePathList[itemtran.name] + "\").GetComponent<" + typeStr + ">();\r\n\t\t";
                }

            }

            //string scriptPath = Application.dataPath + "/Scripts/Homeland/AutoScript/" + selectobj.name + ".cs";
            string scriptPath = EditorUtility.SaveFilePanel("Save Script", "", selectobj.name + ".cs", "cs");

            if (string.IsNullOrEmpty(scriptPath))
            {
                Debug.LogWarning("No valid file path selected. Skipping script creation for " + selectobj.name);
                continue;
            }
            string classStr = "";

            //如果已经存在了脚本，则只替换//auto下方的字符串

            if (File.Exists(scriptPath))

            {

                FileStream classfile = new FileStream(scriptPath, FileMode.Open);

                StreamReader read = new StreamReader(classfile);

                classStr = read.ReadToEnd();

                read.Close();

                classfile.Close();

                File.Delete(scriptPath);

                string splitStr = "//autoStart";
                //classStr = "//autoEnd";
                string autoEndStr = "//autoEnd";

                string unchangeStr = Regex.Split(classStr, splitStr, RegexOptions.IgnoreCase)[0];

                //string changeStr = Regex.Split(AutoBuildTemplate.UIClass, splitStr, RegexOptions.IgnoreCase)[1];
                string autoDownStr = Regex.Split(AutoBuildTemplate.UIClass, splitStr, RegexOptions.IgnoreCase)[1];
                string changeStr = Regex.Split(autoDownStr, autoEndStr, RegexOptions.IgnoreCase)[0];

                string autoendUnchangerStr = Regex.Split(classStr, autoEndStr, RegexOptions.IgnoreCase)[1];


                StringBuilder build = new StringBuilder();

                build.Append(unchangeStr);

                build.Append(splitStr);

                build.Append(changeStr);
                build.Append(autoEndStr);
                build.Append(autoendUnchangerStr);

                classStr = build.ToString();

            }

            else

            {

                classStr = AutoBuildTemplate.UIClass;

            }

            classStr = classStr.Replace("#类名#", selectobj.name);

            classStr = classStr.Replace("#成员#", memberstring);

            classStr = classStr.Replace("#查找#", loadedcontant);

            classStr = classStr.Replace("#继承#", inherit);


            FileStream file = new FileStream(scriptPath, FileMode.CreateNew);

            StreamWriter fileW = new StreamWriter(file, System.Text.Encoding.UTF8);

            fileW.Write(classStr);

            fileW.Flush();

            fileW.Close();

            file.Close();

            Debug.Log($"创建脚本 {scriptPath} 成功");

            AssetDatabase.SaveAssets();

            AssetDatabase.Refresh();
            AddComponent(selectobj);
        }

    }
    static void AddComponent(GameObject selectObj)
    {
        Type componentType = Type.GetType(selectObj.name);
        if (componentType == null)
        {
            // 在当前程序集内查找类型
            componentType = AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(a => a.GetTypes())
                .FirstOrDefault(t => t.Name == selectObj.name);

            if (componentType == null)
            {
                Debug.LogError($"Component '{selectObj.name}' not found.");
            }
        }
        // 检查选中的对象是否已经有该组件
        if (selectObj.GetComponent(componentType) == null)
        {
            // 反射方式添加组件
            selectObj.AddComponent(componentType);
            Debug.Log($"Added component '{selectObj.name}' to '{selectObj.name}'.");
        }
        else
        {
            Debug.LogWarning($"'{selectObj.name}' already has the component '{selectObj.name}' attached.");
        }


    }

}
