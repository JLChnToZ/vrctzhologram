using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace HologramShader {

public class HologramShaderGUI : ShaderGUI
{
    Material _material;
    MaterialProperty[] _props;
    MaterialEditor _materialEditor;

    // Properties

    // Albedo
    private MaterialProperty Albedo = null;
    private MaterialProperty AlbedoColor = null;
    private MaterialProperty AlbedoColor2 = null;
    private MaterialProperty Brightness = null;
    private MaterialProperty Alpha = null;
    private MaterialProperty Direction = null;
    private MaterialProperty MSDFPixelSize = null;

    // Rim
    private MaterialProperty RimColor = null;
    private MaterialProperty RimPower = null;

    // Scanlines
    private MaterialProperty ScanSpeed = null;
    private MaterialProperty ScanTiling = null;

    // Glow
    private MaterialProperty GlowSpeed = null;
    private MaterialProperty GlowTiling = null;

    // Glitch
    private MaterialProperty GlitchSpeed = null;
    private MaterialProperty GlitchIntensity = null;

    // Flicker
    private MaterialProperty Flicker = null;
    private MaterialProperty FlickerSpeed = null;

    private static class Styles
    {
        public static GUIContent AlbedoText = new GUIContent("Albedo");
        public static GUIContent FlickerText = new GUIContent("Flicker Mask");
    }

    enum Category
    {
        General = 0,
        Effects,
    }

    void AssignProperties()
    {
        Albedo = FindProperty("_MainTex", _props);
        AlbedoColor = FindProperty("_MainColor", _props);
        AlbedoColor2 = FindProperty("_MainColor2", _props);
        Brightness = FindProperty("_Brightness", _props);
        Alpha = FindProperty("_Alpha", _props);
        Direction = FindProperty("_Direction", _props);
        MSDFPixelSize = FindProperty("_MainTexMSDFPixelRange", _props);

        RimColor = FindProperty("_RimColor", _props);
        RimPower = FindProperty("_RimPower", _props);

        ScanSpeed = FindProperty("_ScanSpeed", _props);
        ScanTiling = FindProperty("_ScanTiling", _props);

        GlowSpeed = FindProperty("_GlowSpeed", _props);
        GlowTiling = FindProperty("_GlowTiling", _props);

        GlitchSpeed = FindProperty("_GlitchSpeed", _props);
        GlitchIntensity = FindProperty("_GlitchIntensity", _props);

        Flicker = FindProperty("_FlickerTex", _props);
        FlickerSpeed = FindProperty("_FlickerSpeed", _props);
    }

    public override void OnGUI(MaterialEditor materialEditor, MaterialProperty[] props)
    {
        _material = materialEditor.target as Material;
        _props = props;
        _materialEditor = materialEditor;

        AssignProperties();

        Layout.Initialize(_material);

        EditorGUILayout.BeginHorizontal();
        GUILayout.Space(-7);
        EditorGUILayout.BeginVertical();
        EditorGUI.BeginChangeCheck();
        DrawGUI();
        EditorGUILayout.EndVertical();
        GUILayout.Space(1);
        EditorGUILayout.EndHorizontal();

        Undo.RecordObject(_material, "Material Edition");
    }

    static Texture2D bannerTex = null;
    static GUIStyle rateTxt = null;
    static GUIStyle title = null;
    static GUIStyle linkStyle = null;
    static string twitterURL = "https://twitter.com/moj0111";

    void DrawBanner()
    {
        if (bannerTex == null)
            bannerTex = Resources.Load<Texture2D>("banner");

        if (rateTxt == null)
        {
            rateTxt = new GUIStyle();
            rateTxt.alignment = TextAnchor.LowerRight;
            rateTxt.normal.textColor = new Color(0.9f, 0.9f, 0.9f);
            rateTxt.fontSize = 9;
            rateTxt.padding = new RectOffset(0, 1, 0, 1);
        }

        if (title == null)
        {
            title = new GUIStyle(rateTxt);
            title.normal.textColor = new Color(1f, 1f, 1f);
            title.alignment = TextAnchor.MiddleCenter;
            title.fontSize = 19;
        }

        if (linkStyle == null) linkStyle = new GUIStyle();

        if (bannerTex != null)
        {
            GUILayout.Space(3);
            var rect = GUILayoutUtility.GetRect(0, int.MaxValue, 30, 30);
            EditorGUI.DrawPreviewTexture(rect, bannerTex, null, ScaleMode.ScaleAndCrop);
            rateTxt.alignment = TextAnchor.LowerRight;
            EditorGUI.LabelField(rect, "Follow", rateTxt);
            
            EditorGUI.LabelField(rect, "Hologram Shader", title);

            if (GUI.Button(rect, "", linkStyle)) {
                Application.OpenURL(twitterURL);
            }
            GUILayout.Space(3);
        }
    }

    void DrawGUI()
    {
        DrawBanner();

        if (Layout.BeginFold((int)Category.General, "- Surface -"))
            DrawGeneralSettings();
        Layout.EndFold();

        if (Layout.BeginFold((int)Category.Effects, "- Effects -"))
        {
            DrawGeneralEffect();
            DrawRimSettings();
            DrawScanlinesSettings();
            DrawGlowSettings();
            DrawGlitchSettings();
            DrawFlickerSettings();
        }
        Layout.EndFold();
    }

    void DrawGeneralEffect()
    {
        GUILayout.Space(-3);
        GUILayout.Label("General", EditorStyles.boldLabel);
        EditorGUI.indentLevel++;
        var ofs = EditorGUIUtility.labelWidth;
        _materialEditor.SetDefaultGUIWidths();
        _materialEditor.ShaderProperty(Direction, "Direction");
        EditorGUIUtility.labelWidth = ofs;
        EditorGUI.indentLevel--;
    }

    void DrawGeneralSettings()
    {
        GUILayout.Space(-3);
        EditorGUI.indentLevel++;
        var ofs = EditorGUIUtility.labelWidth;
        _materialEditor.SetDefaultGUIWidths();
        bool msdfEnable = Array.IndexOf(_material.shaderKeywords, "_MAIN_TEX_MSDF") != -1;
        EditorGUIUtility.labelWidth = 0;
        if (msdfEnable)
            _materialEditor.TexturePropertySingleLine(Styles.AlbedoText, Albedo, AlbedoColor2, AlbedoColor);
        else
            _materialEditor.TexturePropertySingleLine(Styles.AlbedoText, Albedo, AlbedoColor);
        EditorGUIUtility.labelWidth = ofs;
        _materialEditor.ShaderProperty(Brightness, "Brightness");
        _materialEditor.ShaderProperty(Alpha, "Alpha");
        EditorGUI.BeginChangeCheck();
        msdfEnable = EditorGUILayout.Toggle("As MSDF Texture", msdfEnable);
        if (EditorGUI.EndChangeCheck())
        {
            if (msdfEnable)
                _material.EnableKeyword("_MAIN_TEX_MSDF");
            else
                _material.DisableKeyword("_MAIN_TEX_MSDF");
        }
        if (msdfEnable)
            _materialEditor.ShaderProperty(MSDFPixelSize, "Pixel Size");
        EditorGUI.indentLevel--;
    }

    void DrawRimSettings()
    {
        GUILayout.Space(-3);
        GUILayout.Label("Rim Light", EditorStyles.boldLabel);
        EditorGUI.indentLevel++;
        var ofs = EditorGUIUtility.labelWidth;
        _materialEditor.SetDefaultGUIWidths();
        _materialEditor.ShaderProperty(RimColor, "Color");
        _materialEditor.ShaderProperty(RimPower, "Power");
        EditorGUIUtility.labelWidth = ofs;
        EditorGUI.indentLevel--;
    }

    void DrawScanlinesSettings()
    {
        GUILayout.Space(-3);
        GUILayout.Label("Scanlines", EditorStyles.boldLabel);
        EditorGUI.indentLevel++;

        bool toggle = Array.IndexOf(_material.shaderKeywords, "_SCAN_ON") != -1;
        EditorGUI.BeginChangeCheck();
        toggle = EditorGUILayout.Toggle("Enable", toggle);
        if (EditorGUI.EndChangeCheck())
        {
            if (toggle)
                _material.EnableKeyword("_SCAN_ON");
            else
                _material.DisableKeyword("_SCAN_ON");
        }

        var ofs = EditorGUIUtility.labelWidth;
        _materialEditor.SetDefaultGUIWidths();
        _materialEditor.ShaderProperty(ScanSpeed, "Speed");
        _materialEditor.ShaderProperty(ScanTiling, "Tiling");
        EditorGUIUtility.labelWidth = ofs;
        EditorGUI.indentLevel--;
    }

    void DrawGlowSettings()
    {
        GUILayout.Space(-3);
        GUILayout.Label("Glow", EditorStyles.boldLabel);
        EditorGUI.indentLevel++;

        bool toggle = Array.IndexOf(_material.shaderKeywords, "_GLOW_ON") != -1;
        EditorGUI.BeginChangeCheck();
        toggle = EditorGUILayout.Toggle("Enable", toggle);
        if (EditorGUI.EndChangeCheck())
        {
            if (toggle)
                _material.EnableKeyword("_GLOW_ON");
            else
                _material.DisableKeyword("_GLOW_ON");
        }

        var ofs = EditorGUIUtility.labelWidth;
        _materialEditor.SetDefaultGUIWidths();
        _materialEditor.ShaderProperty(GlowSpeed, "Speed");
        _materialEditor.ShaderProperty(GlowTiling, "Tiling");
        EditorGUIUtility.labelWidth = ofs;
        EditorGUI.indentLevel--;
    }

    void DrawGlitchSettings()
    {
        GUILayout.Space(-3);
        GUILayout.Label("Glitch", EditorStyles.boldLabel);
        EditorGUI.indentLevel++;

        bool toggle = Array.IndexOf(_material.shaderKeywords, "_GLITCH_ON") != -1;
        EditorGUI.BeginChangeCheck();
        toggle = EditorGUILayout.Toggle("Enable", toggle);
        if (EditorGUI.EndChangeCheck())
        {
            if (toggle)
                _material.EnableKeyword("_GLITCH_ON");
            else
                _material.DisableKeyword("_GLITCH_ON");
        }

        var ofs = EditorGUIUtility.labelWidth;
        _materialEditor.SetDefaultGUIWidths();
        _materialEditor.ShaderProperty(GlitchSpeed, "Speed");
        _materialEditor.ShaderProperty(GlitchIntensity, "Intensity");
        EditorGUIUtility.labelWidth = ofs;
        EditorGUI.indentLevel--;
    }

    void DrawFlickerSettings()
    {
        GUILayout.Space(-3);
        GUILayout.Label("Flicker", EditorStyles.boldLabel);
        EditorGUI.indentLevel++;
        var ofs = EditorGUIUtility.labelWidth;
        _materialEditor.SetDefaultGUIWidths();
        EditorGUIUtility.labelWidth = 0;
        _materialEditor.TexturePropertySingleLine(Styles.FlickerText, Flicker, null);
        EditorGUIUtility.labelWidth = ofs;
        _materialEditor.ShaderProperty(FlickerSpeed, "Speed");
        EditorGUI.indentLevel--;
    }
}

public static class Layout
{
    public static void Initialize(Material material)
    {
        Foldout.Initialize(material);
    }

    public static bool BeginFold(int bit, string label)
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        GUILayout.Space(3);
        EditorGUI.indentLevel++;

        Foldout fold = Foldout.Get(bit);
        bool foldState = EditorGUI.Foldout(EditorGUILayout.GetControlRect(),
            fold.state, label, true);
        fold.state = foldState;

        EditorGUI.indentLevel--;
        if (foldState) GUILayout.Space(5);

        EditorGUILayout.BeginHorizontal();
        GUILayout.Space(1);
        EditorGUILayout.BeginVertical();

        return foldState;
    }

    public static void EndFold()
    {
        EditorGUILayout.EndVertical();
        GUILayout.Space(1);
        EditorGUILayout.EndHorizontal();

        GUILayout.Space(3);
        //EditorGUI.indentLevel--;
        EditorGUILayout.EndVertical();
        GUILayout.Space(0);
    }
}

public struct Foldout
{
    static int foldState;
    static Material _material;

    public static void Initialize(Material material)
    {
        foldState = Mathf.RoundToInt(material.GetFloat("_Fold"));
        _material = material;
    }

    public static Foldout Get(int bit)
    {
        return new Foldout { bit = bit };
    }

    public int bit;
    public bool state
    {
        get { return (foldState & (1 << bit)) != 0; }
        set
        {
            foldState = value ? foldState | (1 << bit) : foldState & ~(1 << bit);
            _material.SetFloat("_Fold", foldState);
        }
    }
}

}
