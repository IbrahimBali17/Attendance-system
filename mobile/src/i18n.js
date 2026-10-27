// Tiny i18n helper (English / Arabic). Add keys here; screens call t('key').
const dict = {
  en: {
    appName: 'HU Attendance', signIn: 'Sign in', signingIn: 'Signing in...', login: 'Email or university ID', password: 'Password',
    courses: 'Courses', checkIn: 'Check-in', history: 'History', alerts: 'Alerts', signOut: 'Sign out',
    noCourses: 'You are not enrolled in any course yet.', attendance: 'Attendance', sessions: 'sessions',
    noSession: 'No active session', noSessionHint: 'Your professor has not started a class session yet. Pull to refresh when class begins.',
    refresh: 'Refresh', scanHint: 'Point the camera at the QR code shown by your professor.', checking: 'Verifying location and QR code...',
    success: 'You are checked in!', failed: 'Check-in failed', scanAgain: 'Scan again', cameraNeeded: 'Camera permission is required to scan the QR code.',
    grant: 'Grant permission', locationNeeded: 'Location permission is required to prove you are in the classroom.',
    badQr: 'This is not an attendance QR code.', noAlerts: 'No alerts yet.', noHistory: 'No attendance records yet.',
    present: 'Present', late: 'Late', language: 'العربية',
  },
  ar: {
    appName: 'حضور الجامعة', signIn: 'تسجيل الدخول', signingIn: 'جاري الدخول...', login: 'البريد أو الرقم الجامعي', password: 'كلمة المرور',
    courses: 'المساقات', checkIn: 'تسجيل الحضور', history: 'السجل', alerts: 'التنبيهات', signOut: 'تسجيل الخروج',
    noCourses: 'أنت غير مسجل في أي مساق بعد.', attendance: 'الحضور', sessions: 'محاضرات',
    noSession: 'لا توجد محاضرة نشطة', noSessionHint: 'لم يبدأ المحاضر الجلسة بعد. حدّث الصفحة عند بدء المحاضرة.',
    refresh: 'تحديث', scanHint: 'وجّه الكاميرا نحو رمز QR الذي يعرضه المحاضر.', checking: 'جاري التحقق من الموقع ورمز QR...',
    success: 'تم تسجيل حضورك!', failed: 'فشل تسجيل الحضور', scanAgain: 'مسح مرة أخرى', cameraNeeded: 'يلزم إذن الكاميرا لمسح الرمز.',
    grant: 'منح الإذن', locationNeeded: 'يلزم إذن الموقع لإثبات وجودك في القاعة.',
    badQr: 'هذا ليس رمز حضور.', noAlerts: 'لا توجد تنبيهات.', noHistory: 'لا توجد سجلات حضور.',
    present: 'حاضر', late: 'متأخر', language: 'English',
  },
};

let lang = 'en';
export const getLang = () => lang;
export const setLang = (l) => { lang = l; };
export const t = (key) => dict[lang][key] ?? dict.en[key] ?? key;
