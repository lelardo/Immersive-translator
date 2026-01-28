using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.IO;
using System;

public class ReconstructorDocumento : MonoBehaviour
{
    [Header("Referencias UI")]
    public RawImage fondoDocumento; // Fondo blanco
    public RectTransform contenedorTextos; // Contenedor para los textos
    
    [Header("Prefab")]
    public GameObject prefabTexto; // Prefab de TextMeshPro
    
    [Header("Configuración")]
    public Color colorFondo = Color.white;
    public Color colorTexto = Color.black;
    public float factorEscalaTexto = 1.0f;
    
    private List<GameObject> textosInstanciados = new List<GameObject>();
    private string rutaLog;
    private Vector2 dimensionesOriginales;

    void Start()
    {
        rutaLog = Path.Combine(Application.persistentDataPath, "reconstructor_log.txt");
        EscribirLog("=== RECONSTRUCTOR INICIADO ===");
    }

    public void ReconstruirDocumento(List<LineaConTraduccion> lineas, Vector2 dimensionesImagen)
    {
        EscribirLog($"=== Reconstruyendo documento ===");
        EscribirLog($"Líneas: {lineas.Count}, Dimensiones: {dimensionesImagen.x}x{dimensionesImagen.y}");
        
        dimensionesOriginales = dimensionesImagen;
        
        // Limpiar textos anteriores
        LimpiarTextos();
        
        // Configurar fondo blanco
        ConfigurarFondo();
        
        // Crear cada línea de texto
        foreach (var linea in lineas)
        {
            CrearTexto(linea);
        }
        
        EscribirLog($"✓ Documento reconstruido con {textosInstanciados.Count} líneas");
    }

void ConfigurarFondo()
{
    if (fondoDocumento != null)
    {
        // 1. Corregido el nombre de la variable (sin espacio)
        Texture2D texturaBlanca = new Texture2D(1, 1);
        
        // 2. Pintamos el pixel del color deseado
        texturaBlanca.SetPixel(0, 0, colorFondo);
        texturaBlanca.Apply();
        
        fondoDocumento.texture = texturaBlanca;
        
        // 3. IMPORTANTE: Pon el color del componente en Blanco
        // Si lo pones en 'colorFondo' otra vez, se multiplicarán y se verá oscuro.
        fondoDocumento.color = Color.white; 
        
        EscribirLog("Fondo configurado");
    }
}

    void CrearTexto(LineaConTraduccion linea)
    {
        GameObject textoObj;
        TextMeshProUGUI textComponent;
        
        // Crear desde prefab o crear nuevo
        if (prefabTexto != null)
        {
            textoObj = Instantiate(prefabTexto, contenedorTextos);
            textComponent = textoObj.GetComponent<TextMeshProUGUI>();
        }
        else
        {
            // Crear TextMeshPro dinámicamente
            textoObj = new GameObject($"Linea_{textosInstanciados.Count}");
            textoObj.transform.SetParent(contenedorTextos, false);
            textComponent = textoObj.AddComponent<TextMeshProUGUI>();
        }
        
        // Configurar texto
        textComponent.text = linea.traducido;
        textComponent.color = colorTexto;
        textComponent.fontSize = 24 * factorEscalaTexto;
        textComponent.enableAutoSizing = false;
        textComponent.alignment = TextAlignmentOptions.TopLeft;
        textComponent.overflowMode = TextOverflowModes.Overflow;
        
        // Configurar RectTransform con las coordenadas originales
        RectTransform rectTransform = textoObj.GetComponent<RectTransform>();
        
        // Calcular escala (relación entre contenedor y dimensiones originales)
        float escalaX = contenedorTextos.rect.width / dimensionesOriginales.x;
        float escalaY = contenedorTextos.rect.height / dimensionesOriginales.y;
        
        // Anclar en top-left
        rectTransform.anchorMin = new Vector2(0, 1);
        rectTransform.anchorMax = new Vector2(0, 1);
        rectTransform.pivot = new Vector2(0, 1);
        
        // Posicionar según coordenadas originales (escaladas)
        float posX = linea.posX * escalaX;
        float posY = -linea.posY * escalaY; // Negativo porque Y está invertido
        
        rectTransform.anchoredPosition = new Vector2(posX, posY);
        
        // Tamaño aproximado
        float ancho = linea.ancho > 0 ? linea.ancho * escalaX : 500;
        float alto = linea.alto > 0 ? linea.alto * escalaY : 50;
        
        rectTransform.sizeDelta = new Vector2(ancho, alto);
        
        textosInstanciados.Add(textoObj);
        
        EscribirLog($"Texto creado: '{linea.traducido}' en ({posX:F1}, {posY:F1})");
    }

    void LimpiarTextos()
    {
        foreach (var texto in textosInstanciados)
        {
            Destroy(texto);
        }
        textosInstanciados.Clear();
        EscribirLog("Textos anteriores limpiados");
    }

    public void MostrarOriginal()
    {
        // TODO: Cambiar a mostrar texto original
        EscribirLog("Mostrar original (por implementar)");
    }

    public void MostrarTraduccion()
    {
        // Ya está mostrando la traducción
        EscribirLog("Mostrando traducción");
    }

    void EscribirLog(string mensaje)
    {
        string logLine = $"[{DateTime.Now:HH:mm:ss}] {mensaje}\n";
        Debug.Log("[RECONSTRUCTOR] " + mensaje);
        
        try 
        {
            File.AppendAllText(rutaLog, logLine);
        } 
        catch { }
    }
}