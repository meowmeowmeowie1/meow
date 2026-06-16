@php
    $localesCfg = config('blog.locales');
    $current = app()->getLocale();
    $meta = $localesCfg[$current];

    // hreflang / language-switch alternates. Content pages pass $alternates
    // explicitly (slugs differ per locale); slugless routes are auto-built.
    if (! isset($alternates)) {
        $alternates = [];
        $routeName = \Illuminate\Support\Facades\Route::currentRouteName();
        $params = request()->route() ? request()->route()->parameters() : [];
        foreach (array_keys($localesCfg) as $code) {
            try {
                $alternates[$code] = route($routeName, array_merge($params, ['locale' => $code]));
            } catch (\Throwable $e) {
                // route not resolvable for this locale; skip.
            }
        }
    }

    $pageTitle = ($title ?? config('app.name'));
    $metaDesc = $metaDescription ?? __('messages.tagline');
    $adsenseClient = \App\Models\Setting::get('adsense_client_id');
    $analyticsId = \App\Models\Setting::get('analytics_id');
    $siteName = \App\Models\Setting::get('site_name_'.$current) ?: config('app.name');
@endphp
<!DOCTYPE html>
<html lang="{{ $meta['html_lang'] }}" dir="{{ $meta['dir'] }}">
<head>
    <meta charset="utf-8">
    <meta name="viewport" content="width=device-width, initial-scale=1">
    <title>{{ $pageTitle }}</title>
    <meta name="description" content="{{ $metaDesc }}">
    <link rel="canonical" href="{{ $alternates[$current] ?? url()->current() }}">

    {{-- hreflang alternates linking AR <-> EN counterparts --}}
    @foreach ($alternates as $code => $href)
        <link rel="alternate" hreflang="{{ $localesCfg[$code]['html_lang'] }}" href="{{ $href }}">
    @endforeach
    @if (isset($alternates[config('blog.x_default_locale')]))
        <link rel="alternate" hreflang="x-default" href="{{ $alternates[config('blog.x_default_locale')] }}">
    @endif

    {{-- Open Graph / Twitter --}}
    <meta property="og:type" content="{{ $ogType ?? 'website' }}">
    <meta property="og:title" content="{{ $pageTitle }}">
    <meta property="og:description" content="{{ $metaDesc }}">
    <meta property="og:url" content="{{ url()->current() }}">
    <meta property="og:locale" content="{{ $current === 'ar' ? 'ar_SA' : 'en_US' }}">
    @isset($ogImage)<meta property="og:image" content="{{ $ogImage }}">@endisset
    <meta name="twitter:card" content="{{ isset($ogImage) ? 'summary_large_image' : 'summary' }}">

    {{-- Webfonts: IBM Plex Sans Arabic (AR) + Inter (EN) --}}
    <link rel="preconnect" href="https://fonts.googleapis.com">
    <link rel="preconnect" href="https://fonts.gstatic.com" crossorigin>
    <link rel="stylesheet"
        href="https://fonts.googleapis.com/css2?family=IBM+Plex+Sans+Arabic:wght@400;500;600;700&family=Inter:wght@400;500;600;700&display=swap">

    @stack('head')

    {{-- AdSense loader (once, in <head>); publisher ID comes from settings --}}
    @if ($adsenseClient)
        <script async src="https://pagead2.googlesyndication.com/pagead/js/adsbygoogle.js?client={{ $adsenseClient }}"
            crossorigin="anonymous"></script>
    @endif

    {{-- Analytics (GA4) --}}
    @if ($analyticsId)
        <script async src="https://www.googletagmanager.com/gtag/js?id={{ $analyticsId }}"></script>
        <script>
            window.dataLayer = window.dataLayer || [];
            function gtag(){dataLayer.push(arguments);}
            gtag('js', new Date());
            gtag('config', '{{ $analyticsId }}');
        </script>
    @endif

    @vite(['resources/css/app.css', 'resources/js/app.js'])
</head>
<body class="min-h-screen bg-gray-50 text-gray-900 antialiased">
    @include('partials.header', ['siteName' => $siteName])

    <main class="mx-auto w-full max-w-6xl px-4 py-8">
        @if (session('status'))
            <div class="mb-6 rounded-lg bg-brand-50 px-4 py-3 text-brand-800">{{ session('status') }}</div>
        @endif

        @yield('content')
    </main>

    @include('partials.footer', ['siteName' => $siteName])
</body>
</html>
