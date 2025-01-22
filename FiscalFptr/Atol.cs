// Подробная документация драйвера:
// https://integration.atol.ru/api

using Atol.Drivers10.Fptr;
using RetailCorrector.API;
using RetailCorrector.API.Data;
using RetailCorrector.API.Exceptions;
using RetailCorrector.API.Types;
using System.ComponentModel;

namespace FiscalFptr;

[DisplayName("Atol v.10")]
public class Atol : IFiscal
{
    private readonly Dictionary<int, int> DefaultValues = [];

    private Fptr Driver { get; init; } = new();

    public int CountUnsendDocs
    {
        get
        {
            Driver.setParam(65622, 1);
            Driver.fnQueryData();
            return (int)Driver.getParamInt(65625);
        }
    }

    public int FiscalFormat
    {
        get
        {
            Driver.setParam(65622, 7);
            Driver.fnQueryData();
            return (int)Driver.getParamInt(65629);
        }
    }


    public void CancelReceipt() => Driver.cancelReceipt();

    public void CloseReceipt(Payment payment, Currency total)
    {
        Driver.setParam(65613, (double)total);
        Driver.receiptTotal();

        if ((int)payment.CashSum > 0)
            Payment(0, payment.CashSum);

        if ((int)payment.EcashSum > 0)
            Payment(1, payment.EcashSum);

        if ((int)payment.PrepaidSum > 0)
            Payment(2, payment.PrepaidSum);

        if ((int)payment.PostpaidSum > 0)
            Payment(3, payment.PostpaidSum);

        if ((int)payment.ProvisionSum > 0)
            Payment(4, payment.CashSum);

        Driver.closeReceipt();
    }

    public void CloseSession()
    {
        Driver.setParam(65749, true);
        Driver.setParam(65546, 0);
        Driver.report();
    }

    public void Connect(FiscalConnection data)
    {
        Driver.setSingleSetting("Port", $"{(int)data.Type}");
        var keyAddress = data.Type switch
        {
            FiscalConnType.COM => "ComFile",
            FiscalConnType.USB => "UsbDevicePath",
            FiscalConnType.TCP_IP => "IPAddress",
            FiscalConnType.Bluetooth => "MACAddress",
            _ => throw new NotSupportedException()
        };
        Driver.setSingleSetting(keyAddress, data.Address);
        if (data.Type == FiscalConnType.TCP_IP)
            Driver.setSingleSetting("IPPort", $"{data.Port}");
        Driver.applySingleSettings();
        Driver.open();
    }

    public void Disconnect()
    {
        foreach (var i in DefaultValues)
            SetSettingDevice(i.Key, i.Value);
        DefaultValues.Clear();
        Driver.close();
    }

    public void Free() => Driver.destroy();

    public void OpenReceipt(Receipt receipt)
    {
        Driver.setParam(1178, receipt.Correction.CreatedDate);
        Driver.setParam(1179, receipt.Correction.DocId);
        Driver.utilFormTlv();
        byte[] correctionInfo = Driver.getParamByteArray(65624);

        Driver.setParam(65545, receipt.Operation.CorrectionType);
        Driver.setParam(65572, true);
        Driver.setParam(1173, receipt.Correction.FiscalSign == " " ? 0 : 1);
        Driver.setParam(1174, correctionInfo);
        if(!string.IsNullOrWhiteSpace(receipt.Correction.FiscalSign)) 
            Driver.setParam(1192, receipt.Correction.FiscalSign);
        Driver.openReceipt();
    }

    public void OpenSession()
    {
        Driver.setParam(65749, true);
        Driver.openShift();
    }

    public void Payment(byte type, double sum)
    {
        Driver.setParam(65564, type);
        Driver.setParam(65565, sum);
        Driver.payment();
    }

    public void RegisterItem(Position item)
    {
        Driver.setParam(65631, item.Name);
        Driver.setParam(65632, item.Price);
        Driver.setParam(65633, item.Quantity);
        Driver.setParam(65634, item.Sum);
        Driver.setParam(1212, item.ItemType);
        Driver.setParam(65569, item.TaxRate.Id);
        if (FiscalFormat == 120)
            Driver.setParam(2108, item.MeasureUnit.Id);
        Driver.setParam(1214, item.PayMethod);
        Driver.registration();
    }

    private int GetSettingDevice(int code)
    {
        Driver.setParam(65650, code);
        Driver.readDeviceSetting();
        return (int)Driver.getParamInt(65651);
    }

    private void SetSettingDevice(int code, int value)
    {
        Driver.setParam(65650, code);
        Driver.setParam(65651, value);
        Driver.writeDeviceSetting();
    }

    private void SetTempSettingDevice(int code, int value)
    {
        var current = GetSettingDevice(4);
        DefaultValues.Add(code, current);
        SetSettingDevice(code, value);
    }

    public void FixError()
    {
        switch (Driver.errorCode())
        {
            case 0: // Нет ошибки
                return;
            case 1: // Соединение не установлено
            case 2: // Нет связи
            case 3: // Порт занят
            case 4: // Порт недоступен
            case 6: // Внутренняя ошибка библиотеки
            case 18: // Переполнение счетчика наличности
            case 14: // Не удалось загрузить библиотеку
            case 44: // Нет бумаги
            case 45: // Открыта крышка
                throw new DeviceFatalException(Driver.errorCode(), Driver.errorDescription());
            case 8: // Не найден обязательный параметр
            case 13: // Нeкорректное значение параметра
            case 16: // Неверная цена (сумма)
            case 17: // Неверная количество
            case 48: // Неверный тип чека
            case 52: // Сумма не наличных платежей превышает сумму чека
            case 60: // Неверный вид оплаты
            case 63: // Переполнение итога чека
            case 66: // Чек оплачен не полностью
                throw new ReceiptFormatException(Driver.errorCode(), Driver.errorDescription());
            case 15: // Неизвестная ошибка
                throw new DeviceException(Driver.errorCode(), Driver.errorDescription());
            case 68: // Смена превысила 24 часа
                CancelReceipt();
                CloseSession();
                OpenSession();
                throw new DeviceException(Driver.errorCode(), Driver.errorDescription());
            case 73: // Смена закрыта - операция невозможна
                OpenSession();
                throw new DeviceException(Driver.errorCode(), Driver.errorDescription());
            case 80: // В ККТ нет денег для выплаты
                SetTempSettingDevice(56, 0);
                SetTempSettingDevice(4, 0);
                throw new DeviceException(Driver.errorCode(), Driver.errorDescription());
            case 55: // Предыдущая операция не завершена
                Thread.Sleep(1000);
                throw new DeviceException(Driver.errorCode(), Driver.errorDescription());
            default:
                throw new DeviceFatalException(Driver.errorCode(), Driver.errorDescription());
        }
    }
}
