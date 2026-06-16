@props(['post'])

<article class="group flex flex-col overflow-hidden rounded-2xl border border-gray-200 bg-white transition hover:shadow-md">
    <a href="{{ $post->url() }}" class="block">
        @if ($post->cover_image)
            <img src="{{ $post->cover_image }}" alt="{{ $post->title }}" loading="lazy"
                class="h-44 w-full object-cover">
        @else
            <div class="h-44 w-full bg-gradient-to-br from-brand-100 to-brand-50"></div>
        @endif
    </a>
    <div class="flex flex-1 flex-col p-5">
        @if ($post->category)
            <a href="{{ $post->category->url() }}"
                class="mb-2 inline-block w-fit rounded-full bg-brand-50 px-2.5 py-0.5 text-xs font-medium text-brand-700">
                {{ $post->category->name }}
            </a>
        @endif
        <h3 class="text-lg font-bold leading-snug text-gray-900">
            <a href="{{ $post->url() }}" class="hover:text-brand-700">{{ $post->title }}</a>
        </h3>
        @if ($post->excerpt)
            <p class="mt-2 line-clamp-3 text-sm text-gray-600">{{ $post->excerpt }}</p>
        @endif
        <div class="mt-4 flex items-center gap-2 text-xs text-gray-500">
            <span>{{ $post->author?->name }}</span>
            <span>·</span>
            <span>{{ $post->readingMinutes() }} {{ __('messages.min_read') }}</span>
            @if ($post->published_at)
                <span>·</span>
                <time datetime="{{ $post->published_at->toDateString() }}">{{ $post->published_at->isoFormat('LL') }}</time>
            @endif
        </div>
    </div>
</article>
