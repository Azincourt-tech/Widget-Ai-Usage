using AiUsageWidget.Models;

namespace AiUsageWidget.Services;

/// <summary>
/// Onde a janelinha da bandeja abre. O sistema não informa a posição do ícone, então ela abre junto ao
/// cursor (que acabou de clicar no ícone), encostada na barra de tarefas e sempre dentro da área livre do
/// monitor. Conta só em pixels físicos, sem interface, para ser testada.
/// </summary>
public static class TrayPanelPlacement
{
    /// <param name="workArea">Área livre do monitor do cursor (sem a barra de tarefas).</param>
    /// <param name="cursorX">Cursor, em pixels físicos.</param>
    /// <param name="cursorY">Cursor, em pixels físicos.</param>
    /// <param name="width">Tamanho da janelinha, em pixels físicos.</param>
    /// <param name="height">Tamanho da janelinha, em pixels físicos.</param>
    /// <param name="gap">Folga entre a janelinha e a barra de tarefas (ou a borda da tela).</param>
    /// <returns>Canto superior esquerdo da janelinha.</returns>
    public static (int X, int Y) Place(ScreenRect workArea, int cursorX, int cursorY, int width, int height, int gap)
    {
        int x, y;
        if (cursorX >= workArea.Right)
        {
            // Barra de tarefas à direita: abre à esquerda dela, na altura do cursor.
            x = workArea.Right - width - gap;
            y = cursorY - height / 2;
        }
        else if (cursorX < workArea.Left)
        {
            x = workArea.Left + gap;
            y = cursorY - height / 2;
        }
        else if (cursorY < workArea.Top)
        {
            // Barra de tarefas em cima: abre abaixo dela.
            x = cursorX - width / 2;
            y = workArea.Top + gap;
        }
        else
        {
            // Barra embaixo (o normal) ou cursor dentro da área livre (ícone na área de ícones ocultos,
            // barra que se esconde): abre acima do cursor.
            x = cursorX - width / 2;
            y = Math.Min(cursorY, workArea.Bottom) - height - gap;
        }

        return (Clamp(x, workArea.Left + gap, workArea.Right - width - gap),
                Clamp(y, workArea.Top + gap, workArea.Bottom - height - gap));
    }

    // Janelinha maior que a área: fica no começo (o topo e a esquerda continuam visíveis).
    private static int Clamp(int value, int min, int max) => max < min ? min : Math.Clamp(value, min, max);
}
