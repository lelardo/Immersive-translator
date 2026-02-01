using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.IO;

public class ReconstructorDocumento : MonoBehaviour
{
    [Header("Referencias UI")]
    public RawImage fondoDocumento; 
    public RectTransform contenedorTextos; 
    public GameObject prefabTexto; 
    
    [Header("Configuración")]
    public Color colorFondo = Color.white;
    public Color colorTexto = Color.black;
    public float factorEscalaTexto = 1.0f;
    
    private List<GameObject> textosInstanciados = new List<GameObject>();
    private Vector2 dimensionesOriginales;

    public void ReconstruirDocumento(List<LineaConTraduccion> lineas, Vector2 dimensionesImagen)
    {
        dimensionesOriginales = dimensionesImagen;
        
        LimpiarTextos();
        ConfigurarFondo();
        
        foreach (var linea in lineas)
        {
            CrearTexto(linea);
        }
    }
    void Awake()
        {
            DontDestroyOnLoad(gameObject);
        }

    void ConfigurarFondo()
    {
        if (fondoDocumento != null)
        {
            Texture2D texturaBlanca = new Texture2D(1, 1);
            texturaBlanca.SetPixel(0, 0, colorFondo);
            texturaBlanca.Apply();
            
            fondoDocumento.texture = texturaBlanca;
            // CRÍTICO: Esto asegura que el fondo sea blanco puro y no se mezcle con gris
            fondoDocumento.color = Color.white; 
        }
    }

    void CrearTexto(LineaConTraduccion linea)
    {
        // Seguridad: Si no hay contenedor, no hacemos nada
        if(contenedorTextos == null) return;

        GameObject textoObj;
        TextMeshProUGUI textComponent;
        
        if (prefabTexto != null)
        {
            textoObj = Instantiate(prefabTexto, contenedorTextos);
            textComponent = textoObj.GetComponent<TextMeshProUGUI>();
        }
        else
        {
            textoObj = new GameObject($"Linea_{textosInstanciados.Count}");
            textoObj.transform.SetParent(contenedorTextos, false);
            textComponent = textoObj.AddComponent<TextMeshProUGUI>();
        }
        
        textComponent.text = linea.traducido;
        textComponent.color = colorTexto;
        // Ajustamos un poco el tamaño base para que se lea mejor en móvil
        textComponent.fontSize = 24 * factorEscalaTexto; 
        textComponent.alignment = TextAlignmentOptions.TopLeft;
        textComponent.overflowMode = TextOverflowModes.Overflow;
        
        RectTransform rectTransform = textoObj.GetComponent<RectTransform>();
        
        // Matemáticas de escalado
        float escalaX = contenedorTextos.rect.width / dimensionesOriginales.x;
        float escalaY = contenedorTextos.rect.height / dimensionesOriginales.y;
        
        rectTransform.anchorMin = new Vector2(0, 1);
        rectTransform.anchorMax = new Vector2(0, 1);
        rectTransform.pivot = new Vector2(0, 1);
        
        float posX = linea.posX * escalaX;
        float posY = -linea.posY * escalaY; 
        
        rectTransform.anchoredPosition = new Vector2(posX, posY);
        
        float ancho = linea.ancho > 0 ? linea.ancho * escalaX : 500;
        float alto = linea.alto > 0 ? linea.alto * escalaY : 50;
        
        rectTransform.sizeDelta = new Vector2(ancho, alto);
        
        textosInstanciados.Add(textoObj);
    }

    void LimpiarTextos()
    {
        foreach (var texto in textosInstanciados)
        {
            if(texto != null) Destroy(texto);
        }
        textosInstanciados.Clear();
    }
}