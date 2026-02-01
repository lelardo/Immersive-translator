using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using System.Collections;
using System.Collections.Generic;
using TMPro;

public class ControladorAR : MonoBehaviour
{
    [Header("AR Components")]
    public ARPlaneManager planeManager;
    public ARRaycastManager raycastManager;
    public GameObject cursorGuia;
    public GameObject panelReconstruccion;

    [Header("Referencias Externas")]
    public ControladorOCR controladorOCR; // <<<--- ¡ESTA ERA LA LÍNEA PERDIDA!

    [Header("UI")]
    public TextMeshProUGUI textoInstrucciones; 
    public GameObject botonColocarUI; 

    // Estados
    private bool buscandoSuelo = false;
    private bool cursorEsValido = false;

    void Start()
    {
        // 1. Estado inicial: todo apagado
        planeManager.enabled = false; 
        cursorGuia.SetActive(false);
        panelReconstruccion.SetActive(false);
        
        if(botonColocarUI != null) botonColocarUI.SetActive(false);

        // 2. ¡ARRANCAR LA CÁMARA! (Recuperado)
        StartCoroutine(IniciarSecuenciaAutomatica());
    }

    IEnumerator IniciarSecuenciaAutomatica()
    {
        yield return new WaitForSeconds(1.0f);
        
        if(controladorOCR != null) 
        {
            controladorOCR.IniciarEscaneo();
        }
        else
        {
            Debug.LogError("¡OLVIDASTE ASIGNAR EL CONTROLADOR OCR EN EL INSPECTOR!");
            if(textoInstrucciones) textoInstrucciones.text = "Error: Falta ControladorOCR";
        }
    }

    // Llamado por ControladorOCR al volver de la foto
    public void NotificarRegresoDeCamara()
    {
        if(textoInstrucciones) textoInstrucciones.text = "Apunta al suelo...";
        planeManager.enabled = true; 
        buscandoSuelo = true;
        
        // Mostramos el botón para colocar
        if(botonColocarUI != null) botonColocarUI.SetActive(true);
    }

    void Update()
    {
        if (!buscandoSuelo) return;
        ActualizarCursor();
    }

    void ActualizarCursor()
    {
        var centro = new Vector2(Screen.width / 2, Screen.height / 2);
        var hits = new List<ARRaycastHit>();

        if (raycastManager.Raycast(centro, hits, TrackableType.PlaneWithinPolygon | TrackableType.FeaturePoint))
        {
            cursorGuia.SetActive(true);
            cursorEsValido = true;

            // Corrección visual: Subir 5cm y acostar rotación
            Vector3 posCorregida = hits[0].pose.position;
            posCorregida.y += 0.05f; 
            cursorGuia.transform.position = posCorregida;
            cursorGuia.transform.rotation = Quaternion.Euler(90, 0, 0);

            if(textoInstrucciones) textoInstrucciones.text = "Detectado. Pulsa el botón";
        }
        else
        {
            cursorGuia.SetActive(false);
            cursorEsValido = false;
            if(textoInstrucciones) textoInstrucciones.text = "Buscando superficie...";
        }
    }

    // Función del Botón
    public void ColocarDocumentoManual()
    {
        if (!cursorEsValido || !buscandoSuelo) return;

        panelReconstruccion.SetActive(true);
        panelReconstruccion.transform.position = cursorGuia.transform.position;
        panelReconstruccion.transform.rotation = cursorGuia.transform.rotation;
        
        // Apagar sistema
        buscandoSuelo = false;
        cursorGuia.SetActive(false);
        planeManager.enabled = false; 
        if(botonColocarUI != null) botonColocarUI.SetActive(false);
        
        // Limpiar planos azules
        foreach (var plano in planeManager.trackables) plano.gameObject.SetActive(false);
        
        if(textoInstrucciones) textoInstrucciones.text = "Finalizado";
    }
}