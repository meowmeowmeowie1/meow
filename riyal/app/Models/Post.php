<?php

namespace App\Models;

use Illuminate\Database\Eloquent\Builder;
use Illuminate\Database\Eloquent\Factories\HasFactory;
use Illuminate\Database\Eloquent\Model;
use Illuminate\Database\Eloquent\Relations\BelongsTo;
use Illuminate\Database\Eloquent\Relations\BelongsToMany;
use Illuminate\Database\Eloquent\SoftDeletes;
use Illuminate\Support\Str;

class Post extends Model
{
    use HasFactory, SoftDeletes;

    protected $fillable = [
        'translation_group_id',
        'locale',
        'title',
        'slug',
        'excerpt',
        'body',
        'cover_image',
        'author_id',
        'category_id',
        'status',
        'is_featured',
        'meta_title',
        'meta_description',
        'reading_minutes',
        'published_at',
    ];

    protected function casts(): array
    {
        return [
            'is_featured' => 'boolean',
            'published_at' => 'datetime',
            'views' => 'integer',
            'reading_minutes' => 'integer',
        ];
    }

    // ----- Relationships -----

    public function author(): BelongsTo
    {
        return $this->belongsTo(User::class, 'author_id');
    }

    public function category(): BelongsTo
    {
        return $this->belongsTo(Category::class);
    }

    public function tags(): BelongsToMany
    {
        return $this->belongsToMany(Tag::class);
    }

    // ----- Scopes -----

    public function scopePublished(Builder $query): Builder
    {
        return $query->where('status', 'published')
            ->whereNotNull('published_at')
            ->where('published_at', '<=', now());
    }

    public function scopeLocale(Builder $query, string $locale): Builder
    {
        return $query->where('locale', $locale);
    }

    // ----- Helpers -----

    /**
     * The counterpart post in the other locale (for hreflang + the switcher),
     * matched through the shared translation_group_id.
     */
    public function counterpart(): ?Post
    {
        if (! $this->translation_group_id) {
            return null;
        }

        return static::query()
            ->published()
            ->where('translation_group_id', $this->translation_group_id)
            ->where('id', '!=', $this->id)
            ->first();
    }

    public function url(): string
    {
        return route('posts.show', ['locale' => $this->locale, 'slug' => $this->slug]);
    }

    public function readingMinutes(): int
    {
        if ($this->reading_minutes) {
            return $this->reading_minutes;
        }

        $words = max(str_word_count(strip_tags($this->body)), mb_strlen(strip_tags($this->body)) / 6);

        return max(1, (int) ceil($words / 200));
    }

    public function metaTitle(): string
    {
        return $this->meta_title ?: $this->title;
    }

    public function metaDescription(): string
    {
        return $this->meta_description ?: Str::limit(strip_tags($this->excerpt ?: $this->body), 155);
    }

    public function bodyHtml(): string
    {
        return Str::markdown($this->body ?? '');
    }

    public function getRouteKeyName(): string
    {
        return 'slug';
    }
}
