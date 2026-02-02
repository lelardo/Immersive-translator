using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ReconstructorDocumento : MonoBehaviour
{
    [Header("referencias")]
    public RectTransform contenedorTextos;
    public GameObject prefabTexto;

    [Header("Configuración")]
    public float factorEscala = 1.0f;

    private List<GameObject> instanciados = new List<GameObject>();

    [Header("Ajuste Fino")]
    [Range(0.1f, 2.0f)]
    public float factorAnchoCaja = 1.0f;

    public void ReconstruirDocumento(List<LineaConTraduccion> lineas, Vector2 dimensionesImagen)
    {
        LimpiarTextos();

        if (contenedorTextos == null || lineas == null || lineas.Count == 0) return;

        // 1. Calculamos la escala (Relación Tamaño Pantalla / Tamaño Foto Original)
        float escalaX = contenedorTextos.rect.width / dimensionesImagen.x;
        float escalaY = contenedorTextos.rect.height / dimensionesImagen.y;

        foreach (var linea in lineas)
        {
            GameObject obj = Instantiate(prefabTexto, contenedorTextos);
            RectTransform rt = obj.GetComponent<RectTransform>();
            TMP_Text tmp = obj.GetComponent<TMP_Text>();

            // 2. Colocamos el texto
            tmp.text = linea.traducido;
            tmp.color = Color.black;

            // --- MAGIA AQUÍ: CONFIGURACIÓN ANTISUPERPOSICIÓN ---
            
            // A) Anclajes arriba-izquierda (Estándar para coordenadas de imagen)
            rt.anchorMin = new Vector2(0, 1);
            rt.anchorMax = new Vector2(0, 1);
            rt.pivot = new Vector2(0, 1);

            // B) Calcular Posición y Tamaño de la CAJA (No de la fuente)
            float posX = linea.posX * escalaX;
            float posY = -linea.posY * escalaY; // Negativo porque Unity Y crece hacia arriba
            float ancho = linea.ancho * escalaX * factorAnchoCaja; 
            float alto = linea.alto * escalaY;

            // C) Aplicar dimensiones a la CAJA
            rt.anchoredPosition = new Vector2(posX, posY);
            rt.sizeDelta = new Vector2(ancho, alto);

            // D) EL TRUCO: Decirle a la fuente "Cabe aquí dentro o reduce tu tamaño"
            tmp.enableAutoSizing = true; 
            
            // Límites: Nunca más pequeño que 2, nunca más grande que la altura de su caja
            tmp.fontSizeMin = 2; 
            tmp.fontSizeMax = alto * 0.9f; // 90% de la altura de la caja para dejar margen
            
            // Alineación vertical al medio para centrar en su renglón
            tmp.alignment = TextAlignmentOptions.MidlineLeft;
            
            // Evitar desbordamientos
            tmp.overflowMode = TextOverflowModes.Ellipsis; 
            tmp.enableWordWrapping = false; // Importante: En OCR línea a línea, no queremos saltos de carro
        }
    }

    private void LimpiarTextos()
    {
        foreach (Transform child in contenedorTextos) Destroy(child.gameObject);
    }
}
