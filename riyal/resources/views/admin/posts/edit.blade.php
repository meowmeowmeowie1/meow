@extends('admin.layout')

@php
    $isEdit = $post->exists;
    $action = $isEdit ? route('admin.posts.update', $post) : route('admin.posts.store');
    $tagValue = old('tags', $isEdit ? $post->tags->pluck('name')->join(', ') : '');
@endphp

@section('admin')
<div class="flex items-center justify-between">
    <h1 class="text-2xl font-bold">{{ $isEdit ? 'Edit post' : 'New post' }}</h1>
    @if ($isEdit && $post->status === 'published')
        <a href="{{ $post->url() }}" target="_blank" class="text-sm text-brand-700 hover:underline">View ↗</a>
    @endif
</div>

<form action="{{ $action }}" method="post" class="mt-6 grid gap-6 lg:grid-cols-3">
    @csrf
    @if ($isEdit) @method('PUT') @endif

    <div class="space-y-4 lg:col-span-2">
        <div>
            <label class="mb-1 block text-sm font-medium">Title</label>
            <input name="title" value="{{ old('title', $post->title) }}" required
                class="w-full rounded-lg border border-gray-300 px-4 py-2 focus:border-brand-600 focus:outline-none">
        </div>
        <div>
            <label class="mb-1 block text-sm font-medium">Slug <span class="text-gray-400">(optional — auto from title)</span></label>
            <input name="slug" value="{{ old('slug', $post->slug) }}"
                class="w-full rounded-lg border border-gray-300 px-4 py-2 focus:border-brand-600 focus:outline-none">
        </div>
        <div>
            <label class="mb-1 block text-sm font-medium">Excerpt</label>
            <textarea name="excerpt" rows="2" class="w-full rounded-lg border border-gray-300 px-4 py-2 focus:border-brand-600 focus:outline-none">{{ old('excerpt', $post->excerpt) }}</textarea>
        </div>
        <div>
            <label class="mb-1 block text-sm font-medium">Body <span class="text-gray-400">(Markdown)</span></label>
            <textarea name="body" rows="20" required dir="auto"
                class="w-full rounded-lg border border-gray-300 px-4 py-2 font-mono text-sm focus:border-brand-600 focus:outline-none">{{ old('body', $post->body) }}</textarea>
        </div>

        <fieldset class="rounded-xl border border-gray-200 p-4">
            <legend class="px-1 text-sm font-semibold text-gray-600">SEO</legend>
            <label class="mb-1 block text-sm font-medium">Meta title</label>
            <input name="meta_title" value="{{ old('meta_title', $post->meta_title) }}"
                class="mb-3 w-full rounded-lg border border-gray-300 px-4 py-2 focus:border-brand-600 focus:outline-none">
            <label class="mb-1 block text-sm font-medium">Meta description</label>
            <textarea name="meta_description" rows="2" class="w-full rounded-lg border border-gray-300 px-4 py-2 focus:border-brand-600 focus:outline-none">{{ old('meta_description', $post->meta_description) }}</textarea>
        </fieldset>
    </div>

    <div class="space-y-4">
        <div class="rounded-xl border border-gray-200 bg-white p-4">
            <label class="mb-1 block text-sm font-medium">Locale</label>
            <select name="locale" class="mb-3 w-full rounded-lg border border-gray-300 px-3 py-2">
                <option value="ar" @selected(old('locale', $post->locale)==='ar')>العربية (AR)</option>
                <option value="en" @selected(old('locale', $post->locale)==='en')>English (EN)</option>
            </select>

            <label class="mb-1 block text-sm font-medium">Status</label>
            <select name="status" class="mb-3 w-full rounded-lg border border-gray-300 px-3 py-2">
                <option value="draft" @selected(old('status', $post->status)==='draft')>Draft</option>
                <option value="published" @selected(old('status', $post->status)==='published')>Published</option>
            </select>

            <label class="mb-1 block text-sm font-medium">Publish at <span class="text-gray-400">(schedule)</span></label>
            <input type="datetime-local" name="published_at"
                value="{{ old('published_at', optional($post->published_at)->format('Y-m-d\TH:i')) }}"
                class="mb-3 w-full rounded-lg border border-gray-300 px-3 py-2">

            <label class="flex items-center gap-2 text-sm">
                <input type="checkbox" name="is_featured" value="1" @checked(old('is_featured', $post->is_featured))> Featured
            </label>
        </div>

        <div class="rounded-xl border border-gray-200 bg-white p-4">
            <label class="mb-1 block text-sm font-medium">Category</label>
            <select name="category_id" class="mb-3 w-full rounded-lg border border-gray-300 px-3 py-2">
                <option value="">— None —</option>
                @foreach ($categories as $cat)
                    <option value="{{ $cat->id }}" @selected(old('category_id', $post->category_id)==$cat->id)>[{{ strtoupper($cat->locale) }}] {{ $cat->name }}</option>
                @endforeach
            </select>

            <label class="mb-1 block text-sm font-medium">Tags <span class="text-gray-400">(comma-separated)</span></label>
            <input name="tags" value="{{ $tagValue }}"
                class="w-full rounded-lg border border-gray-300 px-3 py-2 focus:border-brand-600 focus:outline-none">

            <label class="mb-1 mt-3 block text-sm font-medium">Cover image URL</label>
            <input name="cover_image" value="{{ old('cover_image', $post->cover_image) }}"
                class="w-full rounded-lg border border-gray-300 px-3 py-2 focus:border-brand-600 focus:outline-none">
        </div>

        <button class="w-full rounded-lg bg-brand-600 px-4 py-2.5 font-medium text-white hover:bg-brand-700">
            {{ $isEdit ? 'Save changes' : 'Create post' }}
        </button>
    </div>
</form>

@if ($isEdit)
    <div class="mt-8 max-w-2xl rounded-xl border border-gray-200 bg-white p-5">
        <h2 class="text-lg font-semibold">Link translation</h2>
        <p class="mt-1 text-sm text-gray-500">Pair this post with its counterpart in the other locale so they share hreflang and the language switcher.</p>
        <form action="{{ route('admin.posts.link', $post) }}" method="post" class="mt-4 flex gap-2">
            @csrf
            <select name="counterpart_id" class="flex-1 rounded-lg border border-gray-300 px-3 py-2 text-sm">
                <option value="">— Not linked —</option>
                @foreach ($linkCandidates as $cand)
                    <option value="{{ $cand->id }}" @selected($post->translation_group_id && $cand->translation_group_id === $post->translation_group_id)>{{ $cand->title }}</option>
                @endforeach
            </select>
            <button class="rounded-lg border border-gray-300 px-4 py-2 text-sm font-medium hover:border-brand-600 hover:text-brand-700">Save link</button>
        </form>
    </div>
@endif
@endsection
