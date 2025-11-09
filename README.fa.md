# زیرساخت پیام‌رسانی برای لاگ‌های حساس (RabbitMQ)

[![Difficulty](https://img.shields.io/badge/difficulty-%D9%85%D8%AA%D9%88%D8%B3%D8%B7-brightgreen)]()
[![Language](https://img.shields.io/badge/language-C%23-informational)]()
[![Deadline](https://img.shields.io/badge/deadline-1404--08--25-critical)]()

> طراحی و پیاده‌سازی ستون‌فقرات لاگینگ مبتنی بر پیام با RabbitMQ که **همزمان** دو الگو را پشتیبانی کند: (1) **توزیع کار** برای `Error` (هر پیام فقط توسط یک پردازشگر و با بالانس عادلانه)، و (2) **پخش همگانی** برای `Info` (ارسال بلادرنگ به همه مشترکین فعال).

---

## فهرست مطالب

* [پیش‌نیاز](#پیشنیاز)
* [شرح مسئله](#شرح-مسئله)
* [قوانین و محدودیت‌ها](#قوانین-و-محدودیتها)
* [راهنمای معماری](#راهنمای-معماری)
* [مثال ورودی/خروجی](#مثال-ورودیحروجی)
* [نحوه اجرا و تست](#نحوه-اجرا-و-تست)
* [نحوه ارسال (PR)](#نحوه-ارسال-pr)
* [معیارهای ارزیابی](#معیارهای-ارزیابی)
* [زمان‌بندی](#زمانبندی)
* [راه‌های ارتباطی](#راههای-ارتباطی)

---

## پیش‌نیاز

* زبان مجاز: **C# (.NET 6+)**
* سیستم‌عامل: Windows / Linux / macOS
* نصب و راه‌اندازی **RabbitMQ** بر عهدهٔ شرکت‌کننده است (محلی یا ابری). در این مخزن **هیچ Template یا Docker Compose** ارائه نشده است.
* استفاده از کتابخانه رسمی C# یعنی `RabbitMQ.Client`.

---

## شرح مسئله

می‌خواهیم برای یک سیستم زنده و حیاتی، زیرساختی پیام‌محور بسازیم:

* **لاگ‌های حیاتی (`Error`)**: هر پیام باید فقط توسط **یک** Worker فعال پردازش شود و بار بین Workerها **متعادل** گردد (الگوی Work Queue / Competing Consumers).
* **لاگ‌های عمومی (`Info`)**: هر پیام باید به **تمام** سرویس‌های مانیتورینگ متصل، **بلادرنگ** ارسال شود (الگوی Fanout / Pub-Sub).

الزامات:

* تحمل قطعی‌های موقت شبکه (Reconnect/Retry/Backoff، یا Publisher Confirms برای مسیرهای حساس).
* مقیاس‌پذیری افقی بدون توقف سیستم.
* تفکیک شفاف دو جریان و استفادهٔ مشترک از یک کلاستر RabbitMQ (در صورت تمایل).

**خروجی‌های مورد انتظار**

1. **Producer** برای تولید پیام‌های `Error` و `Info` با Payload واقعی/ساخت‌یافته.
2. **Error Workers** (حداقل ۲ نمونه) با توزیع عادلانه، `prefetch` مناسب و **ack دستی**.
3. **Info Subscribers** (حداقل ۲ سرویس مستقل) که هر کدام صف اختصاصی خود را دارند.
4. لاگ‌های ساختاریافته در کنسول برای مشاهدهٔ جریان‌ها.
5. README کوتاه داخل پوشهٔ راه‌حل شما برای توضیح نحوهٔ اجرا.

---

## قوانین و محدودیت‌ها

* استفادهٔ مناسب از Primitiveها:

  * جریان `Error`: صف کاری رقابتی (direct یا default)، صف **Durable**، `prefetch`، **ack دستی**.
  * جریان `Info`: اکسچنج **fanout** با صف جداگانه برای هر مشترک.
* **هیچ Template/Compose آماده‌ای در ریپو وجود ندارد**؛ پیکربندی RabbitMQ و اسکریپت‌های اجرا با خود شماست.
* پایداری مسیرهای حیاتی (صف Durable، پیام Persistent در صورت نیاز).
* عدم هاردکد کردن مقادیر حساس؛ استفاده از **متغیرهای محیطی**.
* کدنویسی خوانا و مدیریت خطا (Reconnect/Retry/Backoff).

---

## راهنمای معماری

* اکسچنج‌ها (پیشنهادی):

  * `logs.error.exchange` (direct) → صف: `logs.error.q` (مصرف‌کنندگان رقابتی)
  * `logs.info.exchange` (fanout) → صف‌ها: `logs.info.q.<service>`
* **`basic.qos(prefetch)`** را برای کنترل فشار روی Workerها تنظیم کنید.
* در صورت امکان از **Publisher Confirms** یا الگوی Retry + Idempotency برای مسیر `Error` استفاده کنید.

---

## مثال ورودی/خروجی

**Producer**

```
[Producer] Sent Error id=E-1023 service=auth msg="DB timeout" severity=HIGH
[Producer] Sent Info  id=I-5541 service=web  msg="GET /api/orders 200" latency_ms=42
```

**Worker خطا (A)**

```
[ErrorWorker-A] E-1023 دریافت شد … در حال پردازش … ack شد
```

**مشترک اطلاعات (grafana)**

```
[InfoSub-grafana] I-5541 -> بروزرسانی داشبورد
```

---

## نحوه اجرا و تست

### 1) کلون ریپو

```bash
git clone https://github.com/dotin-challenge/messaging-infra-challenge.git
cd messaging-infra-challenge
```

### 2) پیش‌نیازهای محیطی

* RabbitMQ باید در دسترس باشد (مثلاً `amqp://user:pass@host:5672/`).
* مقداردهی از طریق **متغیرهای محیطی** (مثال):

  * `AMQP_URI`
  * یا جداگانه: `RABBIT_HOST`, `RABBIT_USER`, `RABBIT_PASS`

### 3) ساختار پوشه‌ها (الزامی)

```
solutions/<language>/<username>/
  ├─ Producer/
  ├─ ErrorWorker/
  └─ InfoSubscriber/
```

> نمونهٔ مسیر C#: `solutions/C#/YOUR_NAME/Producer`

### 4) بیلد و اجرا (C#)

```bash
dotnet build
# پنجره 1: Producer
dotnet run --project solutions/C#/YOUR_NAME/Producer
# پنجره 2 و 3: Error Workers (حداقل دو نمونه)
dotnet run --project solutions/C#/YOUR_NAME/ErrorWorker
dotnet run --project solutions/C#/YOUR_NAME/ErrorWorker
# پنجره 4 و 5: Info Subscribers (نام سرویس را آرگومان بدهید)
dotnet run --project solutions/C#/YOUR_NAME/InfoSubscriber -- grafana
dotnet run --project solutions/C#/YOUR_NAME/InfoSubscriber -- elk
```

### 5) تست‌ها (اختیاری)

```bash
dotnet test
```

---

## نحوه ارسال (PR)

1. ریپو را **Fork** کنید.
2. یک برنچ بسازید:

   ```bash
   git checkout -b solution/<username>
   ```
3. کد را در مسیر زیر قرار دهید:

   ```
   solutions/<language>/<username>/
     ├─ فایل‌های کد
     └─ README.md
   ```
4. یک Pull Request با عنوان زیر باز کنید:

   ```
   [Solution] زیرساخت پیام‌رسانی برای لاگ‌های حساس (RabbitMQ) - <username>
   ```

---

## معیارهای ارزیابی

| معیار                          | درصد   |
| ------------------------------ | ------ |
| درستی عملکرد                   | 40%    |
| کیفیت و خوانایی کد             | 25%    |
| مدیریت خطا و تاب‌آوری          | 10%    |
| قالب خروجی و تجربه توسعه‌دهنده | 10%    |
| مستندسازی                      | 5%     |
| **سرعت ارسال (زمان PR)**       | **5%** |

> هرچه PR شما زودتر و به‌درستی قبل از ددلاین ارسال شود، شانس بیشتری برای کسب این **۵٪** امتیاز اضافه خواهید داشت.

---

## زمان‌بندی

* **شروع:** 1404-08-18
* **پایان ارسال PR:** 1404-08-25

---

## راه‌های ارتباطی

* ریپو: [https://github.com/dotin-challenge/messaging-infra-challenge](https://github.com/dotin-challenge/messaging-infra-challenge)
* GitHub Issues / ایمیل
