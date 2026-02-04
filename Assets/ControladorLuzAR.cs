using UnityEngine;
using UnityEngine.XR.ARFoundation;

[RequireComponent(typeof(Light))]
public class ControladorLuzAR : MonoBehaviour
{
    public ARCameraManager arCameraManager; // Arrastra aquí tu cámara
    private Light miLuz;

    void Awake()
    {
        miLuz = GetComponent<Light>();
    }

    void OnEnable()
    {
        if (arCameraManager != null)
            arCameraManager.frameReceived += ActualizarLuz;
    }

    void OnDisable()
    {
        if (arCameraManager != null)
            arCameraManager.frameReceived -= ActualizarLuz;
    }

    void ActualizarLuz(ARCameraFrameEventArgs args)
    {
        // 1. Ajustar Brillo (Intensidad)
        if (args.lightEstimation.averageBrightness.HasValue)
        {
            // Multiplicamos por un factor (ej 1.5) porque a veces se ve muy oscuro
            miLuz.intensity = args.lightEstimation.averageBrightness.Value * 1.5f; 
        }

        // 2. Ajustar Color (Temperatura de color - calido/frio)
        if (args.lightEstimation.averageColorTemperature.HasValue)
        {
            miLuz.colorTemperature = args.lightEstimation.averageColorTemperature.Value;
        }
        
        // 3. Ajustar Color directo (Si el dispositivo lo soporta)
        if (args.lightEstimation.mainLightColor.HasValue)
        {
            miLuz.color = args.lightEstimation.mainLightColor.Value;
        }
    }
}