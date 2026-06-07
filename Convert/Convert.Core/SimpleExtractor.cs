using SevenZip;
using System.IO.Compression;

namespace Convert.Core
{
    public static class SimpleExtractor
    {
        /// <summary>
        /// Extrait un fichier ZIP ou 7z dans un dossier donné.
        /// </summary>
        public static void Extract(string archivePath, string destinationFolder)
        {
            if (!File.Exists(archivePath))
                throw new FileNotFoundException("Archive introuvable", archivePath);

            Directory.CreateDirectory(destinationFolder);

            string ext = Path.GetExtension(archivePath).ToLowerInvariant();

            switch (ext)
            {
                case ".zip":
                    ExtractZip(archivePath, destinationFolder);
                    break;

                case ".7z":
                    Extract7z(archivePath, destinationFolder);
                    break;

                default:
                    throw new NotSupportedException($"Format non supporté : {ext}");
            }
        }

        // ---------------------------------------------------------------------
        // ZIP (natif .NET)
        // ---------------------------------------------------------------------
        private static void ExtractZip(string archivePath, string destinationFolder)
        {
            ZipFile.ExtractToDirectory(archivePath, destinationFolder, true);
        }

        // ---------------------------------------------------------------------
        // 7z (via LZMA Decoder intégré)
        // ---------------------------------------------------------------------
        private static void Extract7z(string archivePath, string destinationFolder)
        {
            using var input = File.OpenRead(archivePath);

            // Lire les propriétés LZMA (5 octets)
            byte[] properties = new byte[5];
            if (input.Read(properties, 0, 5) != 5)
                throw new InvalidDataException("Archive 7z invalide (header LZMA manquant).");

            // Lire la taille décompressée (8 octets)
            byte[] sizeBytes = new byte[8];
            if (input.Read(sizeBytes, 0, 8) != 8)
                throw new InvalidDataException("Archive 7z invalide (taille manquante).");

            long uncompressedSize = BitConverter.ToInt64(sizeBytes, 0);

            // Le reste du fichier = données compressées
            long compressedSize = input.Length - 13;

            // Préparer le décodeur
            var decoder = new LzmaDecoder();
            var lzma = new SevenZip.Compression.LZMA.Decoder();
            lzma.SetDecoderProperties(properties);

            // Sortie : on extrait uniquement mkvmerge.exe (cas MKVToolNix portable)
            string outputFile = Path.Combine(destinationFolder, "mkvmerge.exe");

            using var output = File.Create(outputFile);

            // Décompression
            lzma.Code(input, output, compressedSize, uncompressedSize, null);
        }
    }
}
