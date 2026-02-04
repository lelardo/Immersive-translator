using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro; // Necesario para acceder al Dropdown

public class MenuManager : MonoBehaviour
{
    [Header("UI References")]
    public TMP_Dropdown dropdownIdioma; // Arrastra aquí tu Dropdown

    // Esta función se llamará al pulsar el botón "Iniciar"
    public void IniciarExperiencia()
    {
        Debug.Log("1. Botón presionado. Iniciando lógica...");

        // PROTECCIÓN: ¿Se te olvidó arrastrar el dropdown?
        if (dropdownIdioma == null)
        {
            Debug.LogError("¡ERROR CRÍTICO! La variable 'Dropdown Idioma' no está asignada en el Inspector.");
            return; // Detenemos todo para que no explote
        }

        // 1. Obtener datos
        int indiceSeleccionado = dropdownIdioma.value;
        string codigoIdioma = "es";

        switch (indiceSeleccionado)
        {
            case 0: codigoIdioma = "es"; break;
            case 1: codigoIdioma = "en"; break;
            case 2: codigoIdioma = "fr"; break;
            case 3: codigoIdioma = "qu"; break;
            default: codigoIdioma = "es"; break;
        }

        // 2. Guardar
        PlayerPrefs.SetString("IdiomaDestino", codigoIdioma);
        PlayerPrefs.SetString("ModoApp", "Traduccion"); 
        PlayerPrefs.Save();

        Debug.Log($"2. Configuración guardada ({codigoIdioma}). Intentando cargar escena 1...");

        // 3. CAMBIAR ESCENA POR NÚMERO (Más seguro que por nombre)
        // Asegúrate que tu escena AR es la número 1 en Build Settings
        SceneManager.LoadScene(1); 
    }
}