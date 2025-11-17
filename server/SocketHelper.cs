using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Sockets;
using System.Text;

namespace server
{
    /// <summary>
    /// Вспомогательный класс для работы с форматом сообщений "длина + данные"
    /// Содержит методы для отправки и получения сообщений через сокеты
    /// </summary>
    public static class SocketMessageHelper
    {
        /// <summary>
        /// Отправляет сообщение в формате "длина + данные" через сокет
        /// </summary>
        /// <param name="socket">Сокет для отправки</param>
        /// <param name="message">Текстовое сообщение для отправки</param>
        /// <exception cref="ArgumentNullException">Если сокет или сообщение null</exception>
        /// <exception cref="SocketException">При ошибках отправки данных</exception>
        public static void SendMessage(Socket socket, string message)
        {
            // Проверяем входные параметры
            if (socket == null)
                throw new ArgumentNullException(nameof(socket), "Сокет не может быть null");

            if (string.IsNullOrEmpty(message))
                throw new ArgumentException("Сообщение не может быть пустым", nameof(message));

            Debug.WriteLine($"[Отправка] Подготовка сообщения: \"{message}\"");

            try
            {
                // Шаг 1: Преобразуем строку в байты с использованием UTF-8 кодировки
                // UTF-8 - стандартная кодировка для текстовых данных в интернете
                byte[] messageBytes = Encoding.UTF8.GetBytes(message);
                Debug.WriteLine($"[Отправка] Размер сообщения в байтах: {messageBytes.Length}");

                // Шаг 2: Создаем префикс с длиной сообщения (4 байта для типа int)
                // BitConverter преобразует int в массив байт
                // Важно: BitConverter использует порядок байт текущей системы (endianness)
                byte[] lengthPrefix = BitConverter.GetBytes(messageBytes.Length);

                // Для сетевой передачи обычно используется big-endian (network byte order)
                // Но для простоты и совместимости клиентов на одной платформе можно использовать системный порядок
                Debug.WriteLine($"[Отправка] Префикс длины (4 байта): {BitConverter.ToString(lengthPrefix)}");

                // Шаг 3: Отправляем префикс длины
                // Всегда отправляем ровно 4 байта
                int bytesSent = socket.Send(lengthPrefix);
                Debug.WriteLine($"[Отправка] Отправлено байт длины: {bytesSent}");

                // Шаг 4: Отправляем само сообщение
                bytesSent = socket.Send(messageBytes);
                Debug.WriteLine($"[Отправка] Отправлено байт данных: {bytesSent}");
                Debug.WriteLine($"[Отправка] Сообщение успешно отправлено: \"{message}\"");
            }
            catch (SocketException ex)
            {
                Debug.WriteLine($"[ОШИБКА] Ошибка сокета при отправке: {ex.SocketErrorCode} - {ex.Message}");
                throw;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ОШИБКА] Неизвестная ошибка при отправке: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Получает сообщение в формате "длина + данные" из сокета
        /// </summary>
        /// <param name="socket">Сокет для получения данных</param>
        /// <returns>Полученное текстовое сообщение</returns>
        /// <exception cref="ArgumentNullException">Если сокет null</exception>
        /// <exception cref="SocketException">При ошибках получения данных или разрыве соединения</exception>
        /// <exception cref="InvalidOperationException">Если получена некорректная длина сообщения</exception>
        public static string ReceiveMessage(Socket socket)
        {
            if (socket == null)
                throw new ArgumentNullException(nameof(socket), "Сокет не может быть null");

            Debug.WriteLine("[Получение] Начало получения сообщения...");

            try
            {
                // Шаг 1: Получаем длину сообщения (ровно 4 байта)
                byte[] lengthBuffer = new byte[4]; // Буфер для хранения 4 байт длины
                int bytesRead = 0; // Счетчик прочитанных байт

                Debug.WriteLine("[Получение] Ожидание получения 4 байт длины сообщения...");
                // Цикл для чтения ровно 4 байт (защита от частичного чтения)
                while (bytesRead < 4)
                {
                    // Receive читает доступные данные из сокета
                    // Параметры:
                    // - lengthBuffer: буфер для чтения
                    // - bytesRead: смещение в буфере (куда записывать новые данные)
                    // - 4 - bytesRead: количество байт для чтения
                    // - SocketFlags.None: без специальных флагов
                    int result = socket.Receive(
                        lengthBuffer,
                        bytesRead,
                        4 - bytesRead,
                        SocketFlags.None
                    );

                    // Если результат 0 - соединение было разорвано удаленной стороной
                    if (result == 0)
                    {
                        Debug.WriteLine("[Получение] Соединение разорвано удаленной стороной");
                        throw new SocketException((int)SocketError.ConnectionReset);
                    }

                    bytesRead += result;
                    Debug.WriteLine($"[Получение] Прочитано байт длины: {result} (всего: {bytesRead}/4)");
                }

                // Шаг 2: Преобразуем 4 байта в целое число (длину сообщения)
                // BitConverter.ToInt32 преобразует первые 4 байта массива в int
                int messageLength = BitConverter.ToInt32(lengthBuffer, 0);
                Debug.WriteLine($"[Получение] Получена длина сообщения: {messageLength} байт");

                // Дополнительная проверка на корректность длины
                if (messageLength <= 0 || messageLength > 10 * 1024 * 1024) // 10 МБ максимум
                {
                    Debug.WriteLine($"[ОШИБКА] Некорректная длина сообщения: {messageLength} байт");
                    throw new InvalidOperationException($"Некорректная длина сообщения: {messageLength}");
                }

                // Шаг 3: Получаем само сообщение (ровно messageLength байт)
                byte[] messageBuffer = new byte[messageLength];
                bytesRead = 0;

                Debug.WriteLine($"[Получение] Ожидание получения {messageLength} байт данных...");

                // Цикл для чтения ровно messageLength байт (защита от частичного чтения)
                while (bytesRead < messageLength)
                {
                    int result = socket.Receive(
                        messageBuffer,
                        bytesRead,
                        messageLength - bytesRead,
                        SocketFlags.None
                    );

                    if (result == 0)
                    {
                        Debug.WriteLine("[Получение] Соединение разорвано во время получения данных");
                        throw new SocketException((int)SocketError.ConnectionReset);
                    }

                    bytesRead += result;
                    Debug.WriteLine($"[Получение] Прочитано байт данных: {result} (всего: {bytesRead}/{messageLength})");
                }

                // Шаг 4: Преобразуем байты в строку с использованием UTF-8 кодировки
                string message = Encoding.UTF8.GetString(messageBuffer);
                Debug.WriteLine($"[Получение] Успешно получено сообщение: \"{message}\"");

                return message;
            }
            catch (SocketException ex)
            {
                Debug.WriteLine($"[ОШИБКА] Ошибка сокета при получении: {ex.SocketErrorCode} - {ex.Message}");
                throw;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ОШИБКА] Неизвестная ошибка при получении: {ex.Message}");
                throw;
            }
        }
    }
}
