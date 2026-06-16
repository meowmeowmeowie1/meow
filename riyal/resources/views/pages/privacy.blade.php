@extends('layouts.app')

@section('content')
<div class="mx-auto max-w-3xl">
    @if ($locale === 'ar')
        <h1 class="text-3xl font-extrabold text-gray-900">سياسة الخصوصية</h1>
        <div class="article-body mt-6">
            <p>تشرح هذه السياسة كيفية تعاملنا مع بياناتك عند زيارة هذا الموقع.</p>
            <h2>البيانات التي نجمعها</h2>
            <p>نجمع بيانات استخدام مجهّلة عبر أدوات التحليل (مثل Google Analytics) لفهم كيفية تصفّح الزوّار للموقع. إذا راسلتنا عبر نموذج التواصل، فإننا نحتفظ بالاسم والبريد والرسالة للرد عليك فقط.</p>
            <h2>ملفات تعريف الارتباط والإعلانات</h2>
            <p>نعرض إعلانات عبر Google AdSense. تستخدم Google وشركاؤها ملفات تعريف الارتباط لعرض إعلانات بناءً على زياراتك لهذا الموقع ومواقع أخرى. يمكنك ضبط تفضيلات الإعلانات من خلال إعدادات إعلانات Google.</p>
            <h2>روابط لأطراف ثالثة</h2>
            <p>قد يحتوي الموقع على روابط لمواقع خارجية لا نتحكم في سياسات خصوصيتها.</p>
            <h2>التواصل</h2>
            <p>لأي استفسار حول الخصوصية، تواصل معنا عبر صفحة «تواصل معنا».</p>
        </div>
    @else
        <h1 class="text-3xl font-extrabold text-gray-900">Privacy Policy</h1>
        <div class="article-body mt-6">
            <p>This policy explains how we handle your data when you visit this site.</p>
            <h2>Data we collect</h2>
            <p>We collect anonymised usage data through analytics tools (such as Google Analytics) to understand how visitors browse the site. If you contact us through the form, we keep your name, email and message solely to reply to you.</p>
            <h2>Cookies and advertising</h2>
            <p>We display ads through Google AdSense. Google and its partners use cookies to serve ads based on your visits to this and other websites. You can control ad personalisation through Google Ads Settings.</p>
            <h2>Third-party links</h2>
            <p>The site may link to external websites whose privacy practices we do not control.</p>
            <h2>Contact</h2>
            <p>For any privacy question, reach us via the Contact page.</p>
        </div>
    @endif
</div>
@endsection
