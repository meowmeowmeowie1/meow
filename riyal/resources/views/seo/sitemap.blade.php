<?php echo '<?xml version="1.0" encoding="UTF-8"?>'."\n"; ?>
<urlset xmlns="http://www.sitemaps.org/schemas/sitemap/0.9" xmlns:xhtml="http://www.w3.org/1999/xhtml">
@foreach ($urls as $url)
    <url>
        <loc>{{ $url['loc'] }}</loc>
        @if (!empty($url['lastmod']))<lastmod>{{ $url['lastmod'] }}</lastmod>@endif
        @foreach ($url['alternates'] as $code => $href)
        <xhtml:link rel="alternate" hreflang="{{ $code }}" href="{{ $href }}"/>
        @endforeach
        @if (isset($url['alternates'][$xDefault]))
        <xhtml:link rel="alternate" hreflang="x-default" href="{{ $url['alternates'][$xDefault] }}"/>
        @endif
    </url>
@endforeach
</urlset>
