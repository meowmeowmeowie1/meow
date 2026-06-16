@extends('layouts.app')

@push('head')
<script type="application/ld+json">
{!! json_encode([
    '@context' => 'https://schema.org',
    '@type' => 'Article',
    'headline' => $post->title,
    'description' => $post->metaDescription(),
    'inLanguage' => $locale,
    'datePublished' => optional($post->published_at)->toAtomString(),
    'dateModified' => optional($post->updated_at)->toAtomString(),
    'image' => $post->cover_image ? [$post->cover_image] : null,
    'author' => ['@type' => 'Person', 'name' => $post->author?->name],
    'publisher' => [
        '@type' => 'Organization',
        'name' => config('app.name'),
    ],
    'mainEntityOfPage' => ['@type' => 'WebPage', '@id' => $post->url()],
], JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES) !!}
</script>
<script type="application/ld+json">
{!! json_encode([
    '@context' => 'https://schema.org',
    '@type' => 'BreadcrumbList',
    'itemListElement' => array_values(array_filter([
        ['@type' => 'ListItem', 'position' => 1, 'name' => __('messages.home'), 'item' => route('home', ['locale' => $locale])],
        $post->category ? ['@type' => 'ListItem', 'position' => 2, 'name' => $post->category->name, 'item' => $post->category->url()] : null,
        ['@type' => 'ListItem', 'position' => $post->category ? 3 : 2, 'name' => $post->title, 'item' => $post->url()],
    ])),
], JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES) !!}
</script>
@endpush

@section('content')
    <article class="mx-auto max-w-3xl">
        <nav class="mb-4 text-sm text-gray-500">
            <a href="{{ route('home', ['locale' => $locale]) }}" class="hover:text-brand-700">{{ __('messages.home') }}</a>
            @if ($post->category)
                <span class="px-1">/</span>
                <a href="{{ $post->category->url() }}" class="hover:text-brand-700">{{ $post->category->name }}</a>
            @endif
        </nav>

        <h1 class="text-3xl font-extrabold leading-tight text-gray-900 md:text-4xl">{{ $post->title }}</h1>

        <div class="mt-4 flex flex-wrap items-center gap-2 text-sm text-gray-500">
            <span>{{ __('messages.by') }} <span class="font-medium text-gray-700">{{ $post->author?->name }}</span></span>
            @if ($post->published_at)
                <span>·</span>
                <time datetime="{{ $post->published_at->toDateString() }}">{{ $post->published_at->isoFormat('LL') }}</time>
            @endif
            <span>·</span>
            <span>{{ $post->readingMinutes() }} {{ __('messages.min_read') }}</span>
        </div>

        @if ($post->cover_image)
            <img src="{{ $post->cover_image }}" alt="{{ $post->title }}"
                class="mt-6 aspect-video w-full rounded-2xl object-cover">
        @endif

        @php
            // Split rendered HTML on paragraph boundaries so we can drop ad
            // units after the intro and around the midpoint of the article.
            $html = $post->bodyHtml();
            $parts = preg_split('/(?<=<\/p>)/', $html);
            $parts = array_values(array_filter($parts, fn ($p) => trim($p) !== ''));
            $count = count($parts);
            $afterIntro = min(2, max(0, $count - 1)); // after ~2nd paragraph
            $mid = (int) floor($count / 2);
        @endphp

        <div class="article-body mt-8">
            @foreach ($parts as $i => $part)
                {!! $part !!}
                @if ($count >= 4 && $i + 1 === $afterIntro)
                    <x-ad placement="in_article" />
                @elseif ($count >= 7 && $i + 1 === $mid)
                    <x-ad placement="mid_article" />
                @endif
            @endforeach
        </div>

        @if ($post->tags->isNotEmpty())
            <div class="mt-8 flex flex-wrap gap-2">
                @foreach ($post->tags as $tag)
                    <a href="{{ $tag->url() }}"
                        class="rounded-full bg-gray-100 px-3 py-1 text-sm text-gray-700 hover:bg-gray-200">#{{ $tag->name }}</a>
                @endforeach
            </div>
        @endif

        <x-ad placement="below_post" />
    </article>

    @if ($related->isNotEmpty())
        <section class="mx-auto mt-12 max-w-5xl">
            <h2 class="mb-4 text-xl font-bold text-gray-900">{{ __('messages.related_articles') }}</h2>
            <div class="grid gap-6 md:grid-cols-3">
                @foreach ($related as $rel)
                    <x-post-card :post="$rel" />
                @endforeach
            </div>
        </section>
    @endif
@endsection
