using UnityEngine;
using UnityEngine.UI;

public sealed partial class CrosshairWidget
{
    private void HideUnusedPieces(Image[] selectedPieces)
    {
        if (defaultPieces != null && defaultPieces != selectedPieces)
        {
            for (int pieceIndex = 0; pieceIndex < defaultPieces.Length; pieceIndex++)
            {
                if (defaultPieces[pieceIndex] != null)
                {
                    Color hiddenColor = defaultPieces[pieceIndex].color;
                    hiddenColor.a = 0f;
                    defaultPieces[pieceIndex].color = hiddenColor;
                }
            }
        }

        if (crosshairs == null)
        {
            return;
        }

        for (int definitionIndex = 0; definitionIndex < crosshairs.Length; definitionIndex++)
        {
            Image[] definitionPieces = crosshairs[definitionIndex]?.pieces;
            if (definitionPieces == null || definitionPieces == selectedPieces)
            {
                continue;
            }

            for (int pieceIndex = 0; pieceIndex < definitionPieces.Length; pieceIndex++)
            {
                if (definitionPieces[pieceIndex] != null)
                {
                    Color hiddenColor = definitionPieces[pieceIndex].color;
                    hiddenColor.a = 0f;
                    definitionPieces[pieceIndex].color = hiddenColor;
                }
            }
        }
    }
}
